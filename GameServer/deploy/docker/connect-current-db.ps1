param(
    [string]$DbName = "gameserver_db",
    [string]$DbUser = "postgres",
    [string]$Query
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Query)) {
    Write-Host "[GameServer] 현재 DB에 인터랙티브 접속합니다: $DbName" -ForegroundColor Cyan
    docker compose exec postgres psql -U $DbUser -d $DbName
}
else {
    Write-Host "[GameServer] 현재 DB에 쿼리 실행: $DbName" -ForegroundColor Cyan
    docker compose exec postgres psql -U $DbUser -d $DbName -c $Query
}
