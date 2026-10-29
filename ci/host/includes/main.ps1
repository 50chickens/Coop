function Get-ExecutionEnvironment
{
    if (-not [string]::IsNullOrEmpty($env:GITHUB_ACTIONS))
    {
        return 'gha'
    }
    return 'local'
}
