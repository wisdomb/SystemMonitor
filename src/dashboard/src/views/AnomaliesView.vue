<template>
  <div class="anomalies-view">

    <!-- Severity guide -->
    <div class="severity-guide">
      <div class="guide-title">Severity reference</div>
      <div class="guide-items">
        <div v-for="s in severityGuide" :key="s.level" class="guide-item">
          <span class="sev-badge" :class="s.level.toLowerCase()">{{ s.level }}</span>
          <div class="guide-text">
            <span class="guide-headline">{{ s.headline }}</span>
            <span class="guide-action">{{ s.action }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Filters -->
    <div class="filter-bar">
      <select v-model="selectedSeverity" class="filter-select">
        <option value="">All Severities</option>
        <option value="Critical">Critical</option>
        <option value="High">High</option>
        <option value="Medium">Medium</option>
        <option value="Low">Low</option>
      </select>

      <select v-model="selectedAgent" class="filter-select">
        <option value="">All Agents</option>
        <option v-for="a in store.agentList" :key="a.agentId" :value="a.agentId">
          {{ a.agentId }}
        </option>
      </select>

      <select v-model="windowHours" class="filter-select">
        <option :value="1">Last 1 hour</option>
        <option :value="6">Last 6 hours</option>
        <option :value="24">Last 24 hours</option>
      </select>

      <button class="btn btn-ghost" @click="refresh">
        <RefreshCw :size="13" /> Refresh
      </button>

      <div class="filter-count">{{ activeFiltered.length }} active</div>
    </div>

    <!-- Status bar: live count + acknowledged toggle -->
    <div class="status-bar">
      <div v-if="store.liveAnomalies.length" class="live-banner">
        <span class="live-dot" />
        {{ store.liveAnomalies.length }} live anomaly(s) detected this session
      </div>

      <button
        v-if="acknowledgedFiltered.length > 0"
        class="ack-toggle"
        @click="showAcknowledged = !showAcknowledged"
      >
        <CheckCircle :size="13" />
        {{ acknowledgedFiltered.length }} acknowledged
        <ChevronDown :size="12" :class="{ rotated: showAcknowledged }" />
      </button>
    </div>

    <!-- Loading skeleton -->
    <div v-if="store.isLoading && store.anomalies.length === 0" class="loading-rows">
      <div v-for="i in 6" :key="i" class="skeleton" style="height:72px;margin-bottom:6px" />
    </div>

    <!-- Active anomaly cards -->
    <div v-else class="anomaly-list">
      <div
        v-for="a in activeFiltered"
        :key="a.id"
        class="anomaly-card"
        :class="`card-${sevClass(a.severity)}`"
      >
        <!-- Severity column -->
        <div class="card-severity">
          <span class="sev-badge" :class="sevClass(a.severity)">
            {{ sevLabel(a.severity) }}
          </span>
          <component :is="sevIcon(a.severity)" :size="18" class="sev-icon" />
        </div>

        <!-- Body -->
        <div class="card-body">
          <div class="card-headline">
            <span class="card-host">{{ a.hostName }}</span>
            <span class="card-sep">·</span>
            <span class="card-agent">{{ a.agentId }}</span>
            <span class="card-time">{{ formatTime(a.detectedAt) }}</span>
          </div>

          <div class="card-description">{{ a.description }}</div>

          <div class="card-meta">
            <span
              v-for="(val, key) in a.affectedMetrics"
              :key="key"
              class="metric-tag"
            >
              {{ metricLabel(String(key)) }}: <strong>{{ Number(val).toFixed(1) }}</strong>
            </span>
            <div class="conf-wrap">
              <span class="conf-label">Confidence</span>
              <div class="conf-bar">
                <div
                  class="conf-fill"
                  :class="confClass(a.confidence)"
                  :style="{ width: (a.confidence * 100) + '%' }"
                />
              </div>
              <span class="conf-pct">{{ (a.confidence * 100).toFixed(0) }}%</span>
            </div>
          </div>

          <!-- Cause + guidance -->
          <div class="card-insight">
            <div class="insight-row cause-row">
              <span class="insight-label">
                <AlertTriangle :size="11" /> Likely cause
              </span>
              <span class="insight-text">{{ sevCause(a.severity, a.description) }}</span>
            </div>
            <div class="insight-row action-row">
              <span class="insight-label">
                <Zap :size="11" /> Recommended action
              </span>
              <span class="insight-text">{{ sevGuidance(a.severity, a.description) }}</span>
            </div>
          </div>
        </div>

        <!-- Actions -->
        <div class="card-actions">
          <button class="btn btn-ghost ack-btn" @click="acknowledge(a.id)">
            <CheckCircle :size="13" />
            Acknowledge
          </button>
        </div>
      </div>

      <div v-if="activeFiltered.length === 0" class="empty-state">
        <p>{{ acknowledgedFiltered.length > 0 ? 'All anomalies acknowledged ✓' : 'No anomalies match current filters' }}</p>
      </div>
    </div>

    <!-- Acknowledged section (collapsible) -->
    <Transition name="slide">
      <div v-if="showAcknowledged && acknowledgedFiltered.length > 0" class="acked-section">
        <div class="acked-header">Acknowledged</div>
        <div class="acked-list">
          <div
            v-for="a in acknowledgedFiltered"
            :key="a.id"
            class="acked-row"
          >
            <CheckCircle :size="12" class="acked-icon" />
            <span class="acked-host">{{ a.hostName }}</span>
            <span class="acked-desc">{{ a.description }}</span>
            <span class="acked-time">{{ formatTime(a.detectedAt) }}</span>
            <button class="undo-btn" @click="unacknowledge(a.id)" title="Move back to active">
              <RotateCcw :size="11" /> Undo
            </button>
          </div>
        </div>
      </div>
    </Transition>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  RefreshCw, CheckCircle, RotateCcw,
  AlertOctagon, AlertTriangle, Info, Bell, ChevronDown, Zap
} from 'lucide-vue-next'
import { formatDistanceToNow } from 'date-fns'
import { useMonitoringStore } from '@/stores/monitoring'

const store            = useMonitoringStore()
const selectedSeverity = ref('')
const selectedAgent    = ref('')
const windowHours      = ref(1)
const acknowledged     = ref<Set<string>>(new Set())
const showAcknowledged = ref(false)

// ── Severity helpers ──────────────────────────────────────────────────────────

const SEV_MAP: Record<string | number, string> = {
  0: 'low', 1: 'medium', 2: 'high', 3: 'critical',
  Low: 'low', Medium: 'medium', High: 'high', Critical: 'critical'
}
const SEV_LABEL: Record<string | number, string> = {
  0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical',
  Low: 'Low', Medium: 'Medium', High: 'High', Critical: 'Critical'
}
function sevClass(s: any) { return SEV_MAP[s]  ?? 'low' }
function sevLabel(s: any) { return SEV_LABEL[s] ?? String(s) }
function sevIcon(s: any) {
  const c = sevClass(s)
  if (c === 'critical') return AlertOctagon
  if (c === 'high')     return AlertTriangle
  if (c === 'medium')   return Bell
  return Info
}

function sevCause(s: any, description: string): string {
  const desc = description.toLowerCase()
  if (desc.includes('cpu'))
    return 'A process is consuming excessive CPU cycles — common causes include infinite loops, heavy computation, a traffic spike, or a runaway background job.'
  if (desc.includes('memory'))
    return 'Memory usage is abnormally high — likely a memory leak, large dataset loaded into RAM, or too many concurrent connections.'
  if (desc.includes('error_rate') || desc.includes('error rate'))
    return 'Requests are failing at an elevated rate — could be a bad deployment, a downstream service outage, or database connectivity issues.'
  if (desc.includes('latency'))
    return 'Response times have degraded — typically caused by slow database queries, network congestion, or an overloaded downstream dependency.'
  if (desc.includes('disk'))
    return 'Disk write throughput is spiking — could be a logging storm, large file writes, database checkpointing, or disk health degradation.'
  if (desc.includes('network'))
    return 'Unusual network traffic detected — possible causes include a DDoS, large data transfer, misconfigured service, or a broadcast storm.'
  return 'An unexpected deviation from the normal baseline was detected by the anomaly detection model.'
}

function sevGuidance(s: any, description: string): string {
  const cls  = sevClass(s)
  const desc = description.toLowerCase()
  if (desc.includes('cpu'))
    return cls === 'critical'
      ? 'Run `top` or Task Manager immediately. Identify and kill the offending process, or scale horizontally if load is legitimate.'
      : 'Monitor for 5–10 minutes. If sustained, check for scheduled jobs or increased traffic and consider scaling.'
  if (desc.includes('memory'))
    return cls === 'critical'
      ? 'Risk of OOM kill is imminent. Restart the service now, then investigate heap dumps or profiler output for the leak source.'
      : 'Check for large in-memory caches or datasets. Review recent code changes that may have introduced a leak.'
  if (desc.includes('error_rate') || desc.includes('error rate'))
    return cls === 'critical'
      ? 'Check application logs immediately. Roll back the last deployment if errors started after a release. Notify on-call.'
      : 'Review error logs for patterns. Check upstream dependencies and database connectivity.'
  if (desc.includes('latency'))
    return cls === 'critical'
      ? 'Check database query performance and downstream service health. Consider circuit-breaking slow dependencies.'
      : 'Profile slow endpoints. Look for N+1 queries, missing indexes, or network timeouts to external APIs.'
  if (desc.includes('disk'))
    return cls === 'critical'
      ? 'Check available disk space. Investigate what is writing heavily — logs, DB, temp files. Clear if safe, alert ops.'
      : 'Review write-heavy processes. Ensure log rotation is configured correctly.'
  return cls === 'critical'
    ? 'Investigate immediately. Check system logs, recent deployments, and downstream dependencies. Escalate if unresolved in 15 minutes.'
    : 'Review system logs and recent changes. Monitor for escalation to a higher severity.'
}

// ── Severity guide ─────────────────────────────────────────────────────────────

const severityGuide = [
  { level: 'Critical', headline: 'Immediate action required',  action: 'System at risk — users likely impacted. Act now.' },
  { level: 'High',     headline: 'Investigate within the hour', action: 'Significant deviation. Could escalate if ignored.' },
  { level: 'Medium',   headline: 'Monitor closely',             action: 'Unusual pattern. Keep an eye on it.' },
  { level: 'Low',      headline: 'Informational',               action: 'Minor deviation. No immediate action needed.' },
]

// ── Metric labels ──────────────────────────────────────────────────────────────

const METRIC_LABELS: Record<string, string> = {
  cpu_percent: 'CPU', memory_percent: 'Memory',
  disk_write_mbps: 'Disk Write', disk_read_mbps: 'Disk Read',
  network_in_mbps: 'Net In', network_out_mbps: 'Net Out',
  error_rate: 'Error Rate', p99_latency_ms: 'P99 Latency',
  requests_per_second: 'Req/s',
}
function metricLabel(key: string) { return METRIC_LABELS[key] ?? key }

// ── Lists ──────────────────────────────────────────────────────────────────────

const allAnomalies = computed(() => [
  ...store.liveAnomalies,
  ...store.anomalies
])

const baseFiltered = computed(() =>
  allAnomalies.value.filter(a => {
    if (selectedSeverity.value && sevLabel(a.severity) !== selectedSeverity.value) return false
    if (selectedAgent.value    && a.agentId !== selectedAgent.value) return false
    return true
  })
)

const activeFiltered       = computed(() =>
  baseFiltered.value.filter(a => !isAcknowledged(a.id))
)
const acknowledgedFiltered = computed(() =>
  baseFiltered.value.filter(a => isAcknowledged(a.id))
)

// ── Acknowledge ────────────────────────────────────────────────────────────────

function isAcknowledged(id: any) { return acknowledged.value.has(String(id)) }
function acknowledge(id: any) {
  acknowledged.value = new Set([...acknowledged.value, String(id)])
  if (acknowledgedFiltered.value.length === 1) showAcknowledged.value = true
}
function unacknowledge(id: any) {
  const s = new Set(acknowledged.value); s.delete(String(id))
  acknowledged.value = s
}

// ── Misc ───────────────────────────────────────────────────────────────────────

function confClass(c: number) {
  if (c >= 0.9) return 'fill-danger'
  if (c >= 0.7) return 'fill-warning'
  return 'fill-accent'
}

function formatTime(ts: string) {
  try { return formatDistanceToNow(new Date(ts), { addSuffix: true }) }
  catch { return ts }
}

function refresh() { store.fetchAnomalies(windowHours.value * 60) }
onMounted(refresh)
</script>

<style scoped>
.anomalies-view { display: flex; flex-direction: column; gap: 16px; }

/* ── Severity guide ────────────────────────────────────────────────── */
.severity-guide {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 14px 18px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.guide-title {
  font-family: var(--mono);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-dim);
}
.guide-items { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; }
.guide-item  { display: flex; align-items: flex-start; gap: 10px; }
.guide-text  { display: flex; flex-direction: column; gap: 2px; }
.guide-headline { font-size: 12px; font-weight: 600; color: var(--text-primary); }
.guide-action   { font-size: 11px; color: var(--text-dim); line-height: 1.5; }

/* ── Filters ───────────────────────────────────────────────────────── */
.filter-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.filter-count { margin-left: auto; font-family: var(--mono); font-size: 11px; color: var(--text-dim); }

/* ── Status bar ────────────────────────────────────────────────────── */
.status-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.live-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px;
  background: color-mix(in srgb, var(--accent) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--accent) 25%, transparent);
  border-radius: 20px;
  font-size: 12px;
  color: var(--accent);
  font-weight: 500;
}
.live-dot {
  width: 7px; height: 7px;
  border-radius: 50%;
  background: var(--accent);
  animation: pulse 1.5s infinite;
}

.ack-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: 20px;
  font-size: 12px;
  color: var(--text-muted);
  cursor: pointer;
  transition: all var(--transition);
}
.ack-toggle:hover { border-color: var(--success); color: var(--success); }
.ack-toggle .rotated { transform: rotate(180deg); }

/* ── Anomaly cards ─────────────────────────────────────────────────── */
.anomaly-list { display: flex; flex-direction: column; gap: 8px; }

.anomaly-card {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-left: 3px solid var(--border);
  border-radius: var(--radius);
  padding: 14px 16px;
  display: flex;
  align-items: flex-start;
  gap: 16px;
  transition: background var(--transition);
}
.anomaly-card:hover { background: var(--bg-hover); }

.card-critical { border-left-color: var(--danger); }
.card-high     { border-left-color: var(--warning); }
.card-medium   { border-left-color: var(--accent); }
.card-low      { border-left-color: var(--border); }

.card-severity {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
  width: 72px;
}
.sev-icon { color: var(--text-dim); }
.card-critical .sev-icon { color: var(--danger); }
.card-high     .sev-icon { color: var(--warning); }
.card-medium   .sev-icon { color: var(--accent); }

.card-body { flex: 1; display: flex; flex-direction: column; gap: 8px; }

.card-headline {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.card-host  { font-family: var(--mono); font-size: 12px; font-weight: 700; color: var(--text-primary); }
.card-agent { font-family: var(--mono); font-size: 11px; color: var(--text-muted); }
.card-sep   { color: var(--text-dim); }
.card-time  { font-size: 11px; color: var(--text-dim); margin-left: auto; }

.card-description { font-size: 13px; color: var(--text-muted); }

.card-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.metric-tag {
  font-family: var(--mono);
  font-size: 11px;
  padding: 2px 8px;
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: 4px;
  color: var(--text-muted);
}
.metric-tag strong { color: var(--text-primary); }

.conf-wrap  { display: flex; align-items: center; gap: 6px; margin-left: auto; }
.conf-label { font-size: 10px; color: var(--text-dim); font-family: var(--mono); }
.conf-bar   { width: 60px; height: 4px; background: var(--bg-surface); border-radius: 2px; overflow: hidden; }
.conf-fill  { height: 100%; border-radius: 2px; transition: width 400ms ease; }
.fill-danger  { background: var(--danger); }
.fill-warning { background: var(--warning); }
.fill-accent  { background: var(--accent); }
.conf-pct { font-family: var(--mono); font-size: 11px; color: var(--text-muted); width: 28px; }

/* Cause + guidance rows */
.card-insight {
  display: flex;
  flex-direction: column;
  gap: 6px;
  background: var(--bg-surface);
  border-radius: var(--radius-sm);
  padding: 10px 12px;
}
.insight-row {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  font-size: 12px;
  line-height: 1.6;
}
.insight-label {
  display: flex;
  align-items: center;
  gap: 4px;
  font-family: var(--mono);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  white-space: nowrap;
  padding-top: 2px;
  flex-shrink: 0;
  width: 120px;
}
.cause-row  .insight-label { color: var(--warning); }
.action-row .insight-label { color: var(--accent); }
.insight-text { color: var(--text-muted); }

.card-actions { flex-shrink: 0; padding-top: 2px; }
.ack-btn { font-size: 11px; padding: 5px 12px; }

.loading-rows { display: flex; flex-direction: column; gap: 6px; }

.acked-section {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}
.acked-header {
  padding: 8px 14px;
  font-family: var(--mono);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-dim);
  border-bottom: 1px solid var(--border);
}
.acked-list { display: flex; flex-direction: column; }
.acked-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 14px;
  border-bottom: 1px solid color-mix(in srgb, var(--border) 50%, transparent);
  font-size: 12px;
}
.acked-row:last-child { border-bottom: none; }
.acked-icon { color: var(--success); flex-shrink: 0; }
.acked-host { font-family: var(--mono); font-size: 11px; color: var(--text-muted); width: 120px; flex-shrink: 0; }
.acked-desc { flex: 1; color: var(--text-dim); font-size: 11px; }
.acked-time { font-size: 10px; color: var(--text-dim); flex-shrink: 0; }
.undo-btn {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 3px 8px;
  background: none;
  border: 1px solid var(--border);
  border-radius: 4px;
  font-size: 10px;
  color: var(--text-dim);
  cursor: pointer;
  transition: all var(--transition);
  flex-shrink: 0;
}
.undo-btn:hover { border-color: var(--text-muted); color: var(--text-muted); }

.slide-enter-active { transition: all 200ms ease; }
.slide-leave-active { transition: all 150ms ease; }
.slide-enter-from, .slide-leave-to { opacity: 0; transform: translateY(-8px); }

@keyframes pulse { 0%,100% { opacity:1; } 50% { opacity:0.4; } }

@media (max-width: 900px) {
  .guide-items { grid-template-columns: repeat(2, 1fr); }
  .anomaly-card { flex-direction: column; }
  .card-severity { flex-direction: row; width: auto; }
  .insight-label { width: auto; }
}
</style>