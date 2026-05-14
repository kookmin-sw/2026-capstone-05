param(
    [switch]$NoBuild,
    [int]$TunnelTimeoutSeconds = 90,
    [string]$BuildOutputPath
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$composeFile = Join-Path $scriptDir "docker-compose.yml"
$tunnelComposeFile = Join-Path $scriptDir "docker-compose.tunnel.yml"
$updateScript = Join-Path $scriptDir "update-auth-api-base-url.ps1"

function Get-BuildConfigTarget {
    param(
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    $expandedPath = [Environment]::ExpandEnvironmentVariables($Path)

    if ([IO.Path]::GetExtension($expandedPath).Equals(".txt", [StringComparison]::OrdinalIgnoreCase)) {
        return $expandedPath
    }

    if ([IO.Path]::GetExtension($expandedPath).Equals(".exe", [StringComparison]::OrdinalIgnoreCase)) {
        return Join-Path (Split-Path -Parent $expandedPath) "auth-api-base-url.txt"
    }

    return Join-Path $expandedPath "auth-api-base-url.txt"
}

$composeArgs = @(
    "compose",
    "-f", $composeFile,
    "-f", $tunnelComposeFile,
    "up",
    "-d"
)

if (-not $NoBuild) {
    $composeArgs += "--build"
}

& docker @composeArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$buildConfigTarget = Get-BuildConfigTarget -Path $BuildOutputPath

if ([string]::IsNullOrWhiteSpace($buildConfigTarget)) {
    $url = & $updateScript -TimeoutSeconds $TunnelTimeoutSeconds
}
else {
    $url = & $updateScript -TimeoutSeconds $TunnelTimeoutSeconds -AdditionalTargets @($buildConfigTarget)
}

Write-Host "[GameServer] Auth API base URL synced: $url" -ForegroundColor Green

if (-not [string]::IsNullOrWhiteSpace($buildConfigTarget)) {
    Write-Host "[GameServer] Build client config synced: $buildConfigTarget" -ForegroundColor Green
}
