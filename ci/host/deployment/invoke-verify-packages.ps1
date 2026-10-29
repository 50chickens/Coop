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
    $configuration | Add-Member -NotePropertyName 'RepositoryRoot' -NotePropertyValue $repositoryRoot -Force
    $configuration | Add-Member -NotePropertyName 'PackageFeedPath' -NotePropertyValue "$repositoryRoot\$($configuration.packageFeedRelativePath)" -Force
    $configuration | Add-Member -NotePropertyName 'StagingPath' -NotePropertyValue "$repositoryRoot\$($configuration.cachedAssetsRelativePath)\verify-packages" -Force
    $version = Get-CoopNextVersion $configuration
    $configuration | Add-Member -NotePropertyName 'CoopVersion' -NotePropertyValue $version -Force
    return $configuration
}

function Invoke-PreflightCheck($configuration)
{
    $configuration.packOrder | ForEach-Object {
        $project = "$($configuration.RepositoryRoot)\src\Coop\$_\$_.csproj"
        if (-not (Test-Path $project))
        {
            throw "Component project not found: $project"
        }
    }
    if (-not (Test-Path $configuration.PackageFeedPath))
    {
        throw "Package feed not found: $($configuration.PackageFeedPath)"
    }
}

function Get-SourceAssembly($configuration, [string] $component)
{
    $built = "$($configuration.RepositoryRoot)\src\Coop\$component\bin\$($configuration.buildConfiguration)\net10.0\$component.dll"
    if (-not (Test-Path $built))
    {
        throw "Built assembly not found: $built. Run the build-test stage first."
    }
    return (Get-FileHash -Path $built -Algorithm SHA256).Hash
}

function Get-FeedAssembly($configuration, [string] $component)
{
    $packagePath = "$($configuration.PackageFeedPath)\$component.$($configuration.CoopVersion).nupkg"
    if (-not (Test-Path $packagePath))
    {
        throw "Package not in feed: $packagePath. The feed is stale; run the packages stage."
    }

    $work = "$($configuration.StagingPath)\$component"
    $null = New-Item -ItemType Directory -Path $work -Force

    $zipPath = "$work\package.zip"
    Copy-Item -Path $packagePath -Destination $zipPath
    Expand-Archive -Path $zipPath -DestinationPath "$work\unpacked"
    $assembly = Get-ChildItem -Path "$work\unpacked\lib" -Filter "$component.dll" -Recurse -File | Select-Object -First 1
    if ($null -eq $assembly)
    {
        throw "Package $component.$($configuration.CoopVersion).nupkg carries no $component.dll"
    }
    return (Get-FileHash -Path $assembly.FullName -Algorithm SHA256).Hash
}

function Invoke-Compare($configuration)
{
    if (Test-Path $configuration.StagingPath)
    {
        Remove-Item -Path $configuration.StagingPath -Recurse -Force
    }

    $stale = 0
    $configuration.packOrder | ForEach-Object {
        $component = $_
        $sourceHash = Get-SourceAssembly $configuration $component
        $feedHash = Get-FeedAssembly $configuration $component
        if ($sourceHash -eq $feedHash)
        {
            Write-Log "$component matches"
        }
        else
        {
            $stale = $stale + 1
            Write-Log "$component DIFFERS: source $($sourceHash.Substring(0, 12)) feed $($feedHash.Substring(0, 12))"
        }
    }

    if ($stale -gt 0)
    {
        throw "$stale package(s) in the feed do not match the built source at version $($configuration.CoopVersion). Remove $($configuration.PackageFeedPath)\*.$($configuration.CoopVersion).nupkg and the same version from the global packages folder, then run the packages stage."
    }
    Write-Log "every package in the feed matches the built source at version $($configuration.CoopVersion)"
}

$configuration = Get-Configuration
$configuration = Add-RuntimeConfiguration $configuration
Invoke-PreflightCheck $configuration
Invoke-Compare $configuration
Write-Log 'verify-packages complete'
