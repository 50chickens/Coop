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
    $configuration | Add-Member -NotePropertyName 'DockerPath' -NotePropertyValue "$repositoryRoot\$($configuration.dockerRelativePath)" -Force
    $configuration | Add-Member -NotePropertyName 'ReleaseContextPath' -NotePropertyValue "$($configuration.releaseRootPath)\$version" -Force
    $configuration | Add-Member -NotePropertyName 'Tag' -NotePropertyValue "$($configuration.dockerImageName):$version" -Force
    return $configuration
}

function Invoke-PreflightCheck($configuration)
{
    $dockerfile = "$($configuration.DockerPath)\Dockerfile"
    if (-not (Test-Path $dockerfile))
    {
        throw "Dockerfile not found: $dockerfile"
    }

    $published = $configuration.ReleaseContextPath
    if (-not (Test-Path $published))
    {
        throw "Published release not found: $published. Run the release stage first."
    }
}

function Invoke-DockerBuild($configuration)
{
    Write-Log "build image $($configuration.Tag)"
    & docker build -t $configuration.Tag -f "$($configuration.DockerPath)\Dockerfile" $configuration.ReleaseContextPath
    if ($LASTEXITCODE -ne 0)
    {
        throw "docker build failed with exit code $LASTEXITCODE"
    }
}

$configuration = Get-Configuration
$configuration = Add-RuntimeConfiguration $configuration
Invoke-PreflightCheck $configuration
Invoke-DockerBuild $configuration
Write-Log 'docker complete'
