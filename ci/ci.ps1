param(
    [string]$Stage = 'all',
    [string]$Environment = 'default'
)

$ErrorActionPreference = 'Stop'
$VerbosePreference = 'SilentlyContinue'
Set-Location $PSScriptRoot

Get-ChildItem -Path "host\includes" -Filter '*.ps1' -Recurse | ForEach-Object {
    Write-Verbose "Dot-sourcing $($_.FullName)"
    . $_.FullName
}

function Get-Configuration
{
    $configPath = "$PSScriptRoot/configs/$Environment.json"

    $configuration = Get-Content $configPath | ConvertFrom-Json
    $configuration | Add-Member -NotePropertyName 'ConfigPath' -NotePropertyValue $configPath -Force
    $configuration | Add-Member -NotePropertyName 'SelectedStage' -NotePropertyValue $Stage -Force

    return $configuration
}

function Add-RuntimeConfiguration($configuration)
{
    $repositoryRoot = (Get-Item "$PSScriptRoot\..").FullName
    $configuration | Add-Member -NotePropertyName 'RepositoryRoot' -NotePropertyValue $repositoryRoot -Force
    $configuration | Add-Member -NotePropertyName 'DeploymentPath' -NotePropertyValue "$PSScriptRoot\host\deployment" -Force
    return $configuration
}

function Get-StageScriptName([string]$name)
{
    if ($name -eq 'build-test')
    {
        return 'build-test.ps1'
    }
    return "invoke-$name.ps1"
}

function Invoke-PreflightCheck($configuration)
{
    if (-not (Test-Path $configuration.ConfigPath))
    {
        throw "Config not found: $($configuration.ConfigPath)"
    }

    $selected = $configuration.SelectedStage
    if ($selected -ne 'all' -and $configuration.stages -notcontains $selected)
    {
        throw "Unknown stage '$selected'. Known stages: $($configuration.stages -join ', '), all"
    }

    $configuration.stages | ForEach-Object {
        $scriptPath = "$($configuration.DeploymentPath)\$(Get-StageScriptName $_)"
        if (-not (Test-Path $scriptPath))
        {
            throw "Stage script not found: $scriptPath"
        }
    }
}

function Get-SelectedStages($configuration)
{
    if ($configuration.SelectedStage -eq 'all')
    {
        return $configuration.stages
    }
    return @($configuration.SelectedStage)
}

function Invoke-Stage($configuration, [string]$name)
{
    Write-Log "=== $name ==="
    $scriptPath = "$($configuration.DeploymentPath)\$(Get-StageScriptName $name)"
    & $scriptPath
    if ($LASTEXITCODE -ne 0)
    {
        throw "$name failed with exit code $LASTEXITCODE"
    }
}

$configuration = Get-Configuration
$configuration = Add-RuntimeConfiguration $configuration
Invoke-PreflightCheck $configuration

(Get-SelectedStages $configuration) | ForEach-Object {
    Invoke-Stage $configuration $_
}

Write-Log '=== CI complete ==='
