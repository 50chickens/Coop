function Get-CoopVersionList($configuration)
{
    return Get-ChildItem -Path $configuration.releaseRootPath -Directory |
        Where-Object { $_.Name -match '^[0-9]+(\.[0-9]+){0,2}$' } |
        ForEach-Object {
            $parts = @($_.Name -split '\.') + @('0', '0')
            [System.Version]::new([int]$parts[0], [int]$parts[1], [int]$parts[2])
        } |
        Sort-Object
}

function Get-CoopNextVersion($configuration)
{
    $released = @(Get-CoopVersionList $configuration)
    if ($released.Count -eq 0)
    {
        return [System.Version]::new(1, 0, 0)
    }
    $highest = $released[-1]
    return [System.Version]::new($highest.Major, $highest.Minor, $highest.Build + 1)
}

function Get-CoopReleasedVersion($configuration)
{
    $released = @(Get-CoopVersionList $configuration)
    if ($released.Count -eq 0)
    {
        throw "no released version under $($configuration.releaseRootPath); the release stage writes one"
    }
    return $released[-1]
}
