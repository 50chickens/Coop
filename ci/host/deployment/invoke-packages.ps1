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
    $configuration | Add-Member -NotePropertyName 'CoopVersion' -NotePropertyValue (Get-CoopNextVersion $configuration) -Force
    return $configuration
}

function Get-PackOrder($configuration)
{
    $componentsRoot = "$($configuration.RepositoryRoot)\src\Coop"
    $ordered = $configuration.packOrder

    return $ordered | ForEach-Object {
        "$componentsRoot\$_\$_.csproj"
    }
}

function Invoke-PreflightCheck($configuration)
{
    $projects = Get-PackOrder $configuration
    $projects | ForEach-Object {
        if (-not (Test-Path $_))
        {
            throw "Component project not found: $_"
        }
    }
}

function Invoke-Pack($configuration)
{
    Write-Log "pack components to $($configuration.packageFeedRelativePath) at version $($configuration.CoopVersion)"
    (Get-PackOrder $configuration) | ForEach-Object {
        & dotnet pack $_ -c $configuration.buildConfiguration -o $configuration.PackageFeedPath --nologo -p:CoopVersion=$($configuration.CoopVersion)
        if ($LASTEXITCODE -ne 0)
        {
            throw "dotnet pack failed for $_ with exit code $LASTEXITCODE"
        }
        Write-Log "packed $(Split-Path -Leaf (Split-Path -Parent $_))"
    }
}

function Invoke-Report($configuration)
{
    $packages = Get-ChildItem -Path $configuration.PackageFeedPath -Filter '*.nupkg' -File
    Write-Log "packages in feed: $($packages.Count)"
}

$configuration = Get-Configuration
$configuration = Add-RuntimeConfiguration $configuration
Invoke-PreflightCheck $configuration
Invoke-Pack $configuration
Invoke-Report $configuration
Write-Log 'packages complete'
