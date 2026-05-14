param(
    [int]$LogTail = 300,
    [int]$TimeoutSeconds = 60,
    [int]$PollIntervalSeconds = 2,
    [string[]]$AdditionalTargets = @()
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path (Join-Path $scriptDir "..\..\..")
$composeFile = Join-Path $scriptDir "docker-compose.yml"
$tunnelComposeFile = Join-Path $scriptDir "docker-compose.tunnel.yml"

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$url = $null

do {
    $logs = docker compose -f $composeFile -f $tunnelComposeFile logs --tail $LogTail cloudflared 2>$null
    $url = $logs |
        Select-String -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' -AllMatches |
        ForEach-Object { $_.Matches.Value } |
        Select-Object -Last 1

    if (-not [string]::IsNullOrWhiteSpace($url)) {
        break
    }

    if ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds $PollIntervalSeconds
    }
} while ((Get-Date) -lt $deadline)

if ([string]::IsNullOrWhiteSpace($url)) {
    throw "Cloudflare tunnel URL was not found within $TimeoutSeconds seconds. Start the tunnel first, then run this script again."
}

$targets = @(
    Join-Path $projectRoot "auth-api-base-url.txt"
    Join-Path $projectRoot "Assets\StreamingAssets\auth-api-base-url.txt"
)

if ($AdditionalTargets.Count -gt 0) {
    $targets += $AdditionalTargets
}

foreach ($target in $targets) {
    $targetDir = Split-Path -Parent $target
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir | Out-Null
    }

    Set-Content -NoNewline -Path $target -Value $url
}

Write-Output $url
