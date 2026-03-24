# Seed Data

Ready-to-use data files for bootstrapping the platform without waiting for real agents.

## Files

### `training-labeled.csv`
2,000 rows of labeled telemetry spanning 5.5 hours (10-second intervals).
- **7.3% anomaly rate** — realistic class imbalance for training
- Anomaly types injected: CPU spike, memory spike, error rate burst, latency spike, disk write spike
- Use this to train the AI model immediately on first run

**Upload via dashboard:** Training Data page → Browse File → Upload  
**Upload via CLI:**
```bash
curl -X POST http://localhost:5000/api/v1/ingest/training-data \
  -H "Content-Type: text/csv" \
  --data-binary @seed-data/training-labeled.csv
```

---

## DataSeeder CLI

The `SystemMonitor.DataSeeder` project gives you three modes:

### Backfill — push historical data (bulk, fast)
```bash
cd src/SystemMonitor.DataSeeder
dotnet run -- --mode=backfill --agents=3 --hours=48 --api=http://localhost:5000
```
Pushes 48 hours × 3 agents × 360 ticks/hour = **51,840 metric events** in seconds.

### Stream — continuous live mock data
```bash
dotnet run -- --mode=stream --agents=3 --api=http://localhost:5000
```
Sends a new batch every 10 seconds. Ctrl+C to stop. Anomalies fire ~3% of ticks.

### Training upload — generate + upload labeled CSV
```bash
dotnet run -- --mode=training --api=http://localhost:5000
```
Prompts for record count, generates a labeled CSV, and POSTs it to the training endpoint.

### All-in-one (backfill then stream)
```bash
dotnet run -- --mode=all --agents=5 --hours=24 --api=http://localhost:5000
```

### Interactive mode (default)
```bash
dotnet run -- --api=http://localhost:5000
```
Presents a menu to choose the mode at runtime.

---

## Mock vs Real Data in the Agent

Set in `appsettings.json` or via environment variable:

```json
"MockData": {
  "Enabled": true,
  "AnomalyProbability": 0.03,
  "LogBurstProbability": 0.02
}
```

Or via env var (Docker / K8s):
```
MockData__Enabled=true
```

`AnomalyProbability` — chance per tick that an anomaly is injected (0–1).  
`LogBurstProbability` — chance per tick that a log error burst fires (0–1).

---

## Docker Compose mock agents

`docker-compose.yml` includes a `mock-agent` service pre-configured with `MockData__Enabled=true`.
Run it alongside the real stack:
```bash
docker compose up mock-agent
```
