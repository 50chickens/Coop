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
    $configuration | Add-Member -NotePropertyName 'MockRoot' -NotePropertyValue "$repositoryRoot\$($configuration.integrationMockRelativePath)" -Force
    $configuration | Add-Member -NotePropertyName 'MockProjectPath' -NotePropertyValue "$repositoryRoot\$($configuration.integrationMockRelativePath)\$($configuration.integrationMockProjectName)\$($configuration.integrationMockProjectName).csproj" -Force
    $configuration | Add-Member -NotePropertyName 'MockOutputPath' -NotePropertyValue "$repositoryRoot\$($configuration.integrationMockRelativePath)\output" -Force
    return $configuration
}

function Invoke-PreflightCheck($configuration)
{
    if (-not (Test-Path $configuration.MockProjectPath))
    {
        throw "Integration mock project not found: $($configuration.MockProjectPath)"
    }
    if (Test-Path $configuration.MockOutputPath)
    {
        Remove-Item -Path $configuration.MockOutputPath -Recurse -Force
    }
    $null = New-Item -ItemType Directory -Path $configuration.MockOutputPath -Force
}

function Invoke-MockTurn($configuration)
{
    Write-Log 'run one scripted agent turn against the real execution engine'
    & dotnet run --project $configuration.MockProjectPath -c $configuration.buildConfiguration --no-build -- $configuration.MockOutputPath
    if ($LASTEXITCODE -ne 0)
    {
        throw "integration mock failed with exit code $LASTEXITCODE"
    }
}

function Invoke-Report($configuration)
{
    Get-ChildItem -Path $configuration.MockOutputPath -File |
        ForEach-Object { Write-Log "artefact $($_.Name) $($_.Length) bytes" }
}

$configuration = Get-Configuration
$configuration = Add-RuntimeConfiguration $configuration
Invoke-PreflightCheck $configuration
Invoke-MockTurn $configuration
Invoke-Report $configuration
Write-Log 'integration-mock complete'
