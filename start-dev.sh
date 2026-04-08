#!/bin/bash

echo ""
echo "  SysMonitor - Local Dev Startup"
echo "  --------------------------------"
echo ""

cd /mnt/c/SystemMonitor || exit

echo "  [1/5] Checking Docker..."
if ! docker info > /dev/null 2>&1; then
    echo "  ERROR: Docker Desktop is not running."
    exit 1
fi
echo "  OK - Docker is running"

echo "  [2/5] Starting Cosmos + Azurite..."
docker compose up cosmos-emulator azurite -d
echo "  OK - Containers starting"

echo "  [3/5] Waiting for Cosmos..."
elapsed=0
while [ $elapsed -lt 120 ]; do
    sleep 5
    elapsed=$((elapsed + 5))
    if docker logs cosmos 2>&1 | grep -q "Started 11/11 partitions"; then
        echo "  OK - Cosmos ready in ${elapsed}s"
        break
    fi
    echo "  ... waiting ${elapsed}s"
done

echo "  [4/5] Starting backend services..."

# API
cd /mnt/c/SystemMonitor/src/SystemMonitor.Api || exit
dotnet run > api.log 2>&1 &

# Worker
cd /mnt/c/SystemMonitor/src/SystemMonitor.Worker || exit
dotnet run > worker.log 2>&1 &

# Agent
cd /mnt/c/SystemMonitor/src/SystemMonitor.Agent || exit
dotnet run > agent.log 2>&1 &

echo "  Waiting for API to warm up..."
sleep 15

echo "  [5/5] Starting dashboard..."

cd /mnt/c/SystemMonitor/src/dashboard || exit
npm run dev > dashboard.log 2>&1 &

sleep 5

echo ""
echo "  --------------------------------"
echo "  All services running in background!"
echo "  Dashboard : http://localhost:5173"
echo "  Swagger   : http://localhost:5000/swagger"
echo ""

# Optional: open browser (Windows)
cmd.exe /c start http://localhost:5173 > /dev/null 2>&1