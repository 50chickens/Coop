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
    $configuration | Add-Member -NotePropertyName 'SolutionPath' -NotePropertyValue "$repositoryRoot\$($configuration.solutionRelativePath)" -Force
    $configuration | Add-Member -NotePropertyName 'BudgetSeconds' -NotePropertyValue $configuration.buildTestBudgetSeconds -Force
    $configuration | Add-Member -NotePropertyName 'CoopVersion' -NotePropertyValue (Get-CoopNextVersion $configuration) -Force
    return $configuration
}

function Invoke-PreflightCheck($configuration)
{
    if (-not (Test-Path $configuration.SolutionPath))
    {
        throw "Solution not found: $($configuration.SolutionPath)"
    }
    if ($configuration.BudgetSeconds -le 0)
    {
        throw "buildTestBudgetSeconds must be greater than zero, found $($configuration.BudgetSeconds)"
    }
}

function Invoke-Restore($configuration)
{
    Write-Log "restore $($configuration.solutionRelativePath) at version $($configuration.CoopVersion)"
    & dotnet restore $configuration.SolutionPath -p:CoopVersion=$($configuration.CoopVersion)
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }
}

function Invoke-Build($configuration)
{
    Write-Log "build $($configuration.solutionRelativePath) $($configuration.buildConfiguration) with warnings as errors"
    & dotnet build $configuration.SolutionPath -c $configuration.buildConfiguration --no-restore -warnaserror -p:CoopVersion=$($configuration.CoopVersion)
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}

function Invoke-Test($configuration)
{
    Write-Log "test $($configuration.solutionRelativePath)"
    & dotnet test $configuration.SolutionPath -c $configuration.buildConfiguration --no-build --nologo -p:CoopVersion=$($configuration.CoopVersion)
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet test failed with exit code $LASTEXITCODE"
    }
}

function Invoke-BudgetedStep($configuration, [string]$name, $stopwatch, $action)
{
    $before = $stopwatch.Elapsed
    & $action
    $taken = $stopwatch.Elapsed - $before
    Write-Log "$name took $([math]::Round($taken.TotalSeconds,1)) s, total $([math]::Round($stopwatch.Elapsed.TotalSeconds,1)) s of $($configuration.BudgetSeconds) s"

    if ($stopwatch.Elapsed.TotalSeconds -gt $configuration.BudgetSeconds)
    {
        throw "build-test exceeded its $($configuration.BudgetSeconds) second budget: $([math]::Round($stopwatch.Elapsed.TotalSeconds,1)) s spent, finishing '$name'. A build that grows past its budget is the signal to look at what the pipeline is compiling rather than to raise the budget."
    }
}

$configuration = Get-Configuration
$configuration = Add-RuntimeConfiguration $configuration
Invoke-PreflightCheck $configuration

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

Invoke-BudgetedStep $configuration 'restore' $stopwatch { Invoke-Restore $configuration }
Invoke-BudgetedStep $configuration 'build' $stopwatch { Invoke-Build $configuration }
Invoke-BudgetedStep $configuration 'test' $stopwatch { Invoke-Test $configuration }

Write-Log 'build-test complete'
