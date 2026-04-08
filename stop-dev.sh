echo ""
echo "  Stopping SysMonitor dev services..."
echo ""

cd /mnt/c/SystemMonitor || exit

echo "  Stopping Docker containers..."
docker compose stop cosmos-emulator azurite > /dev/null 2>&1
echo "  ✓ Containers stopped"

DOTNET_PIDS=$(pgrep -f "dotnet run")

if [ -n "$DOTNET_PIDS" ]; then
    echo "$DOTNET_PIDS" | xargs kill -9
    COUNT=$(echo "$DOTNET_PIDS" | wc -l)
    echo "  ✓ dotnet processes stopped ($COUNT)"
else
    echo "  . No dotnet processes running"
fi

NODE_PIDS=$(pgrep -f "npm run dev")

if [ -n "$NODE_PIDS" ]; then
    echo "$NODE_PIDS" | xargs kill -9
    COUNT=$(echo "$NODE_PIDS" | wc -l)
    echo "  ✓ Node processes stopped ($COUNT)"
else
    echo "  . No node processes running"
fi

echo ""
echo "  All services stopped."
echo ""