# SysMonitor Local Dev Startup
# Usage: powershell -ExecutionPolicy Bypass -File .\start-dev.ps1

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

# Check Docker is running
Write-Host "  [1/5] Checking Docker..." -ForegroundColor White
$dockerCheck = docker info 2>$null
if (-not $?) {
    Write-Host "  ERROR: Docker Desktop is not running. Start it first." -ForegroundColor Red
    Read-Host "  Press Enter to exit"
    exit 1
}
Write-Host "  OK - Docker is running" -ForegroundColor Green

# Start emulators
Write-Host "  [2/5] Starting Cosmos + Azurite..." -ForegroundColor White
docker compose up cosmos-emulator azurite -d | Out-Null
Write-Host "  OK - Containers started" -ForegroundColor Green

# Wait for Cosmos
Write-Host "  [3/5] Waiting for Cosmos (up to 120s)..." -ForegroundColor White
$maxWait = 120
$elapsed = 0
$ready = $false
while ($elapsed -lt $maxWait) {
    Start-Sleep 5
    $elapsed += 5
    $log = docker logs cosmos 2>&1 | Select-String "Started 11/11 partitions"
    if ($log) {
        Write-Host "  OK - Cosmos ready in $elapsed seconds" -ForegroundColor Green
        $ready = $true
        break
    }
    Write-Host "  ... waiting $elapsed of $maxWait seconds" -ForegroundColor DarkGray
}
if (-not $ready) {
    Write-Host "  WARNING: Cosmos not ready yet, continuing anyway" -ForegroundColor Yellow
}

# Start API
Write-Host "  [3/5] Starting API on localhost:5000..." -ForegroundColor White
$apiPath = Join-Path $Root "src\SystemMonitor.Api"
Start-Process powershell -ArgumentList "-NoExit -Command cd '\'; dotnet run" -WindowStyle Normal
Write-Host "  OK - API window opened" -ForegroundColor Green
Write-Host "  Waiting 15s for API to initialise..." -ForegroundColor DarkGray
Start-Sleep 15

# Start Worker
Write-Host "  [4/5] Starting Worker..." -ForegroundColor White
$workerPath = Join-Path $Root "src\SystemMonitor.Worker"
Start-Process powershell -ArgumentList "-NoExit -Command cd '\'; dotnet run" -WindowStyle Normal
Write-Host "  OK - Worker window opened" -ForegroundColor Green

# Start Agent
Write-Host "  [4/5] Starting Agent..." -ForegroundColor White
$agentPath = Join-Path $Root "src\SystemMonitor.Agent"
Start-Process powershell -ArgumentList "-NoExit -Command cd '\'; dotnet run" -WindowStyle Normal
Write-Host "  OK - Agent window opened" -ForegroundColor Green

# Start Dashboard
Write-Host "  [5/5] Starting Dashboard on localhost:5173..." -ForegroundColor White
$dashPath = Join-Path $Root "src\dashboard"
Start-Process powershell -ArgumentList "-NoExit -Command cd '\'; npm run dev" -WindowStyle Normal
Write-Host "  OK - Dashboard window opened" -ForegroundColor Green

Write-Host ""
Write-Host "  --------------------------------" -ForegroundColor DarkGray
Write-Host "  All services starting!" -ForegroundColor Cyan
Write-Host "  Dashboard : http://localhost:5173" -ForegroundColor White
Write-Host "  Swagger   : http://localhost:5000/swagger" -ForegroundColor White
Write-Host "  Login     : admin@sysmonitor.dev / Admin123!" -ForegroundColor White
Write-Host ""

Start-Sleep 8
Start-Process "http://localhost:5173"
