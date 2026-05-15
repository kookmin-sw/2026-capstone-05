param(
    [Parameter(Mandatory = $true)]
    [string]$BuildOutputPath,
    [int]$TunnelTimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$updateScript = Join-Path $scriptDir "update-auth-api-base-url.ps1"

function Get-BuildConfigTarget {
    param(
        [string]$Path
    )

    $expandedPath = [Environment]::ExpandEnvironmentVariables($Path)

    if ([IO.Path]::GetExtension($expandedPath).Equals(".txt", [StringComparison]::OrdinalIgnoreCase)) {
        return $expandedPath
    }

    if ([IO.Path]::GetExtension($expandedPath).Equals(".exe", [StringComparison]::OrdinalIgnoreCase)) {
        return Join-Path (Split-Path -Parent $expandedPath) "auth-api-base-url.txt"
    }

    return Join-Path $expandedPath "auth-api-base-url.txt"
}

$buildConfigTarget = Get-BuildConfigTarget -Path $BuildOutputPath
$url = & $updateScript -TimeoutSeconds $TunnelTimeoutSeconds -AdditionalTargets @($buildConfigTarget)

Write-Host "[GameServer] Auth API base URL synced: $url" -ForegroundColor Green
Write-Host "[GameServer] Build client config synced: $buildConfigTarget" -ForegroundColor Green
