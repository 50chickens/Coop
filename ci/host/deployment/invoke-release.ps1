$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'
$env:MSBUILDDISABLENODEREUSE = '1'
Set-Location $PSScriptRoot

Get-ChildItem -Path "..\includes" -Filter '*.ps1' -Recurse | ForEach-Object {
    . $_.FullName
}

function Get-Configuration
{
    $configPath = "$PSScriptRoot/../../configs/default.json"
    $configuration = Get-Content $configPath | ConvertFrom-Json
    $configuration | Add-Member -NotePropertyName 'ConfigPath' -NotePropertyValue $configPath -Force
    return $configuration
}

function Add-RuntimeConfiguration($configuration)
{
    $repositoryRoot = (Get-Item "$PSScriptRoot\..\..\..").FullName
    $version = Get-CoopNextVersion $configuration
    $configuration | Add-Member -NotePropertyName 'RepositoryRoot' -NotePropertyValue $repositoryRoot -Force
    $configuration | Add-Member -NotePropertyName 'Version' -NotePropertyValue $version -Force
    $configuration | Add-Member -NotePropertyName 'ServerProjectPath' -NotePropertyValue "$repositoryRoot\$($configuration.serverProjectRelativePath)" -Force
    $configuration | Add-Member -NotePropertyName 'ToolProjectPath' -NotePropertyValue "$repositoryRoot\$($configuration.toolProjectRelativePath)" -Force
    $configuration | Add-Member -NotePropertyName 'VersionPath' -NotePropertyValue "$($configuration.releaseRootPath)\$version" -Force
    $configuration | Add-Member -NotePropertyName 'StagingPath' -NotePropertyValue "$($configuration.releaseRootPath)\$($version).staging" -Force
    $configuration | Add-Member -NotePropertyName 'ToolPath' -NotePropertyValue "$($configuration.StagingPath)\tool" -Force
    $configuration | Add-Member -NotePropertyName 'WrapperPath' -NotePropertyValue "$($configuration.StagingPath)\coop.cmd" -Force
    $configuration | Add-Member -NotePropertyName 'DataPath' -NotePropertyValue "$($configuration.releaseRootPath)\data" -Force
    return $configuration
}

function Invoke-PreflightCheck($configuration)
{
    if (-not (Test-Path $configuration.ServerProjectPath))
    {
        throw "Server project not found: $($configuration.ServerProjectPath)"
    }
    if (-not (Test-Path $configuration.ToolProjectPath))
    {
        throw "Tool project not found: $($configuration.ToolProjectPath)"
    }
    if (Test-Path $configuration.VersionPath)
    {
        throw "version $($configuration.Version) is already released at $($configuration.VersionPath), and a released version is never replaced because a running host may be serving it"
    }
}

function Invoke-PrepareStaging($configuration)
{
    if (Test-Path $configuration.StagingPath)
    {
        Remove-Item -Path $configuration.StagingPath -Recurse -Force
        Write-Log "removed leftover $($configuration.StagingPath)"
    }
    $null = New-Item -ItemType Directory -Path $configuration.StagingPath -Force
    Write-Log "staging $($configuration.StagingPath)"
}

function Invoke-Publish($configuration)
{
    Write-Log "publish server $($configuration.Version) to $($configuration.StagingPath)"
    & dotnet publish $configuration.ServerProjectPath -c $configuration.buildConfiguration -o $configuration.StagingPath --nologo -p:CoopVersion=$($configuration.Version)
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

function Invoke-PublishTool($configuration)
{
    Write-Log "publish tool $($configuration.Version) to $($configuration.ToolPath)"
    & dotnet publish $configuration.ToolProjectPath -c $configuration.buildConfiguration -o $configuration.ToolPath --nologo -p:CoopVersion=$($configuration.Version)
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

function Invoke-WriteHostScript($configuration)
{
    $script = @'
Set-Location $PSScriptRoot

$ErrorActionPreference = 'Stop'
$pipeName = $env:COOP_PIPE_NAME
if ([string]::IsNullOrWhiteSpace($pipeName))
{
    $pipeName = 'coop-compile'
}
$pipePath = '\\.\pipe\' + $pipeName
$readySeconds = 30
$stopSeconds = 10
$probeMatching = 0
$probeAbsent = 2
$probeStale = 3

function Test-Pipe
{
    return ([System.IO.Directory]::GetFiles('\\.\pipe\') -contains $pipePath)
}

function Get-ProbeState
{
    $null = & dotnet "$PSScriptRoot\Coop.Tool.dll" probe
    return $LASTEXITCODE
}

function Start-Host
{
    $null = New-Item -ItemType Directory -Path $env:COOP_DATA_ROOT -Force
    $toolArguments = "`"$PSScriptRoot\Coop.Tool.dll`" host"
    $null = Start-Process -FilePath 'dotnet' -ArgumentList $toolArguments -WindowStyle Hidden -RedirectStandardError "$env:COOP_DATA_ROOT\host.log"
    $deadline = [DateTime]::UtcNow.AddSeconds($readySeconds)
    while ((Test-Pipe) -eq $false)
    {
        if ([DateTime]::UtcNow -gt $deadline)
        {
            throw "the compile host did not open $pipePath within $readySeconds seconds"
        }
        Start-Sleep -Milliseconds 100
    }
}

function Wait-PipeClosed([int]$seconds)
{
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    while (Test-Pipe)
    {
        if ([DateTime]::UtcNow -gt $deadline)
        {
            return $false
        }
        Start-Sleep -Milliseconds 100
    }
    return $true
}

function Remove-StaleHost
{
    $null = & dotnet "$PSScriptRoot\Coop.Tool.dll" stop
    if (Wait-PipeClosed $stopSeconds)
    {
        return
    }
    $stale = @(Get-CimInstance -ClassName Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object { ($_.CommandLine -like '*Coop.Tool.dll*') -and ($_.CommandLine -like '* host*') -and ($_.CommandLine -notlike "*$PSScriptRoot*") })
    if ($stale.Count -eq 0)
    {
        throw "the host on $pipePath is another build and does not answer a stop request; stop it by hand or set COOP_PIPE_NAME to a different pipe"
    }
    $stale | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    if ((Wait-PipeClosed $readySeconds) -eq $false)
    {
        throw "the host holding $pipePath did not release the pipe within $readySeconds seconds"
    }
}

if (Test-Pipe)
{
    $state = Get-ProbeState
    if ($state -eq $probeMatching)
    {
        exit 0
    }
    if ($state -eq $probeAbsent)
    {
        throw "a process holds $pipePath but did not answer a version probe; it is busy or wedged and cannot be replaced safely; stop it or set COOP_PIPE_NAME to a different pipe"
    }
    if ($state -ne $probeStale)
    {
        throw "the version probe returned $state rather than $probeMatching, $probeAbsent or $probeStale"
    }
    Remove-StaleHost
}

Start-Host

if ((Get-ProbeState) -ne $probeMatching)
{
    throw "the host on $pipePath does not serve the version of this build"
}

exit 0
'@
    $hostPath = "$($configuration.ToolPath)\coop-host.ps1"
    Set-Content -Path $hostPath -Value $script -Encoding ascii
    Write-Log "host launcher $hostPath"
}

function Invoke-WriteWrapper($configuration)
{
    $wrapper = @"
@echo off
set COOP_DATA_ROOT=$($configuration.DataPath)
powershell -NoProfile -ExecutionPolicy Bypass -File "$($configuration.VersionPath)\tool\coop-host.ps1"
if errorlevel 1 exit /b 1
dotnet "$($configuration.VersionPath)\tool\Coop.Tool.dll" %*
"@
    Set-Content -Path $configuration.WrapperPath -Value $wrapper -Encoding ascii
    Write-Log "wrapper $($configuration.WrapperPath) with data root $($configuration.DataPath)"
}

function Invoke-VerifyRelease($configuration, [string]$rootPath)
{
    $serverAssembly = "$($rootPath)\Coop.Services.Surface.dll"
    if (-not (Test-Path $serverAssembly))
    {
        throw "published server assembly missing: $serverAssembly"
    }
    $toolAssembly = "$($rootPath)\tool\Coop.Tool.dll"
    if (-not (Test-Path $toolAssembly))
    {
        throw "published tool assembly missing: $toolAssembly"
    }
    $hostPath = "$($rootPath)\tool\coop-host.ps1"
    if (-not (Test-Path $hostPath))
    {
        throw "published host launcher missing: $hostPath"
    }
    $wrapperPath = "$($rootPath)\coop.cmd"
    if (-not (Test-Path $wrapperPath))
    {
        throw "published wrapper missing: $wrapperPath"
    }
    $wrapperText = Get-Content -Path $wrapperPath -Raw
    if ($wrapperText.Contains($configuration.StagingPath))
    {
        throw "published wrapper names the staging folder rather than the version folder: $wrapperPath"
    }
}

function Invoke-PromoteStaging($configuration)
{
    Move-Item -Path $configuration.StagingPath -Destination $configuration.VersionPath
    $files = Get-ChildItem -Path $configuration.VersionPath -File
    Write-Log "released $($configuration.Version) to $($configuration.VersionPath), $($files.Count) files"
}

$configuration = Get-Configuration
$configuration = Add-RuntimeConfiguration $configuration
Invoke-PreflightCheck $configuration
Invoke-PrepareStaging $configuration
Invoke-Publish $configuration
Invoke-PublishTool $configuration
Invoke-WriteHostScript $configuration
Invoke-WriteWrapper $configuration
Invoke-VerifyRelease $configuration $configuration.StagingPath
Invoke-PromoteStaging $configuration
Write-Log 'release complete'
