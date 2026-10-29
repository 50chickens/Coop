$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'
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
    $version = Get-CoopReleasedVersion $configuration
    $configuration | Add-Member -NotePropertyName 'RepositoryRoot' -NotePropertyValue $repositoryRoot -Force
    $configuration | Add-Member -NotePropertyName 'ServerPublishPath' -NotePropertyValue "$($configuration.releaseRootPath)\$($version)" -Force
    $configuration | Add-Member -NotePropertyName 'ServerAssemblyPath' -NotePropertyValue "$($configuration.releaseRootPath)\$($version)\Coop.Services.Surface.dll" -Force
    $configuration | Add-Member -NotePropertyName 'DataRoot' -NotePropertyValue "$repositoryRoot\temp\data" -Force
    return $configuration
}

function Invoke-PreflightCheck($configuration)
{
    if (-not (Test-Path $configuration.ServerAssemblyPath))
    {
        throw "Deployed server assembly not found: $($configuration.ServerAssemblyPath). Run the release stage first."
    }
}

function Invoke-DeployedServer($configuration)
{
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'dotnet'
    $startInfo.WorkingDirectory = $configuration.ServerPublishPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add($configuration.ServerAssemblyPath)
    $startInfo.EnvironmentVariables['COOP_DATA_ROOT'] = $configuration.DataRoot

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $ready = $false
    while (-not $process.StandardError.EndOfStream)
    {
        $line = $process.StandardError.ReadLine()
        Write-Log "server: $line"
        if ($line -eq 'ready')
        {
            $ready = $true
            break
        }
    }

    $process.StandardInput.Close()
    $process.Kill($true)
    $process.WaitForExit()

    if (-not $ready)
    {
        throw 'the deployed server never reported ready on standard error'
    }
}

$configuration = Get-Configuration
$configuration = Add-RuntimeConfiguration $configuration
Invoke-PreflightCheck $configuration
Invoke-DeployedServer $configuration
Write-Log 'deployment-test complete'
