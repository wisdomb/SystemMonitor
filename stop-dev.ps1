# SysMonitor — Stop all dev services
# Run from C:\SystemMonitor with: .\stop-dev.ps1

Write-Host ""
Write-Host "  Stopping SysMonitor dev services..." -ForegroundColor Yellow
Write-Host ""

# Stop Docker emulators
Write-Host "  Stopping Docker containers..." -ForegroundColor Gray
docker compose stop cosmos-emulator azurite 2>$null
Write-Host "  ✓ Containers stopped" -ForegroundColor Green

# Kill dotnet processes (API, Worker, Agent)
$dotnetProcs = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
if ($dotnetProcs) {
    $dotnetProcs | Stop-Process -Force
    Write-Host "  ✓ dotnet processes stopped ($($dotnetProcs.Count))" -ForegroundColor Green
} else {
    Write-Host "  . No dotnet processes running" -ForegroundColor DarkGray
}

# Kill npm/node (dashboard)
$nodeProcs = Get-Process -Name "node" -ErrorAction SilentlyContinue
if ($nodeProcs) {
    $nodeProcs | Stop-Process -Force
    Write-Host "  ✓ Node processes stopped ($($nodeProcs.Count))" -ForegroundColor Green
} else {
    Write-Host "  . No node processes running" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "  All services stopped." -ForegroundColor Gray
Write-Host ""
