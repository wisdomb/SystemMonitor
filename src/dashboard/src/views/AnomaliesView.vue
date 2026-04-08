<template>
  <div class="anomalies-view">

    <!-- Tabs -->
    <div class="tabs">
      <button class="tab" :class="{ active: activeTab === 'schema' }" @click="activeTab = 'schema'">
        <GitBranch :size="13" />
        Attribute Changes
        <span class="tab-count" :class="criticalSchemaCount > 0 ? 'count-danger' : ''">
          {{store.schemaEvents.filter(e => !store.isAcknowledged(e.id)).length}}
        </span>
      </button>
      <button class="tab" :class="{ active: activeTab === 'anomalies' }" @click="activeTab = 'anomalies'">
        <Activity :size="13" />
        System Anomalies
        <span class="tab-count">
          {{ activeAnomalies.length }}
        </span>
      </button>
    </div>

    <div v-if="acknowledgedItems.length > 0" class="acked-panel">
      <div class="acked-panel-header">
        <CheckCircle :size="13" />
        {{ acknowledgedItems.length }} acknowledged
        <button class="acked-collapse" @click="showAcked = !showAcked">
          <ChevronDown :size="12" :class="{ rotated: showAcked }" />
        </button>
      </div>
      <Transition name="slide">
        <div v-if="showAcked" class="acked-list">
          <div v-for="item in acknowledgedItems" :key="item.id" class="acked-row">
            <CheckCircle :size="11" class="acked-icon" />
            <span class="acked-label">{{ ackedLabel(item) }}</span>
            <span class="acked-time">{{ formatTime(item.detectedAt ?? item.detectedAt) }}</span>
            <button class="undo-btn" @click="store.unacknowledgeItem(item.id)">
              <RotateCcw :size="10" /> Undo
            </button>
          </div>
        </div>
      </Transition>
    </div>

    <template v-if="activeTab === 'schema'">

      <div class="severity-guide">
        <div class="guide-title">How confidence maps to severity</div>
        <div class="guide-items">
          <div v-for="s in schemaGuide" :key="s.level" class="guide-item">
            <span class="sev-badge" :class="s.level.toLowerCase()">{{ s.level }}</span>
            <div class="guide-text">
              <span class="guide-headline">{{ s.headline }}</span>
              <span class="guide-action">{{ s.action }}</span>
            </div>
          </div>
        </div>
      </div>

      <div class="filter-bar">
        <select v-model="schemaAgent" class="filter-select">
          <option value="">All Agents</option>
          <option v-for="a in store.agentList" :key="a.agentId" :value="a.agentId">
            {{ a.agentId }}
          </option>
        </select>
        <select v-model="schemaTier" class="filter-select">
          <option value="">All Tiers</option>
          <option value="Fuzzy">Fuzzy Match</option>
          <option value="Alias">Alias</option>
          <option value="OpenAI">OpenAI</option>
          <option value="Unresolved">Unresolved</option>
        </select>
        <div class="filter-count">{{ activeSchemaEvents.length }} active</div>
      </div>

      <div class="card-list">
        <div v-for="evt in activeSchemaEvents" :key="evt.id" class="anomaly-card" :class="`card-${schemaClass(evt)}`">
          <div class="card-severity">
            <span class="sev-badge" :class="schemaClass(evt)">
              {{ schemaLabel(evt) }}
            </span>
            <component :is="schemaIcon(evt)" :size="18" class="sev-icon" />
          </div>

          <div class="card-body">
            <div class="card-headline">
              <span class="card-host">{{ evt.hostName }}</span>
              <span class="card-sep">·</span>
              <span class="card-agent">{{ evt.agentId }}</span>
              <span class="card-time">{{ formatTime(evt.detectedAt) }}</span>
            </div>

            <div class="attr-change">
              <span class="attr-raw">{{ evt.rawAttribute }}</span>
              <span class="attr-arrow">→</span>
              <span v-if="evt.wasResolved" class="attr-resolved">{{ evt.resolvedAttribute }}</span>
              <span v-else class="attr-unresolved">Could not resolve</span>
            </div>

            <div class="card-meta">
              <span class="metric-tag">
                Tier: <strong>{{ evt.resolutionTier }}</strong>
              </span>
              <span v-if="evt.wasResolved" class="metric-tag">
                Confidence: <strong>{{ (evt.confidence * 100).toFixed(0) }}%</strong>
              </span>
            </div>

            <div class="card-insight">
              <div class="insight-row cause-row">
                <span class="insight-label">
                  <AlertTriangle :size="11" /> Likely cause
                </span>
                <span class="insight-text">{{ schemaCause(evt) }}</span>
              </div>
              <div class="insight-row action-row">
                <span class="insight-label">
                  <Zap :size="11" /> Action
                </span>
                <span class="insight-text">{{ schemaAction(evt) }}</span>
              </div>
            </div>
          </div>

          <div class="card-actions">
            <button class="btn btn-ghost ack-btn" @click="store.acknowledgeItem(evt.id)">
              <CheckCircle :size="13" /> Acknowledge
            </button>
          </div>
        </div>

        <div v-if="activeSchemaEvents.length === 0" class="empty-state">
          <p>No unacknowledged attribute changes</p>
        </div>
      </div>
    </template>

    <template v-if="activeTab === 'anomalies'">

      <div class="severity-guide">
        <div class="guide-title">Severity reference</div>
        <div class="guide-items">
          <div v-for="s in anomalyGuide" :key="s.level" class="guide-item">
            <span class="sev-badge" :class="s.level.toLowerCase()">{{ s.level }}</span>
            <div class="guide-text">
              <span class="guide-headline">{{ s.headline }}</span>
              <span class="guide-action">{{ s.action }}</span>
            </div>
          </div>
        </div>
      </div>

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
        <select v-model="windowHours" class="filter-select" @change="refresh">
          <option :value="1">Last 1 hour</option>
          <option :value="6">Last 6 hours</option>
          <option :value="24">Last 24 hours</option>
        </select>
        <button class="btn btn-ghost" @click="refresh">
          <RefreshCw :size="13" /> Refresh
        </button>
        <div class="filter-count">{{ activeAnomalies.length }} active</div>
      </div>

      <div v-if="store.liveAnomalies.length" class="live-banner">
        <span class="live-dot" />
        {{ store.liveAnomalies.length }} live anomaly(s) this session
      </div>

      <div v-if="store.isLoading && store.anomalies.length === 0" class="loading-rows">
        <div v-for="i in 4" :key="i" class="skeleton" style="height:72px;margin-bottom:6px" />
      </div>

      <div v-else class="card-list">
        <div v-for="a in activeAnomalies" :key="a.id" class="anomaly-card" :class="`card-${sevClass(a.severity)}`">
          <div class="card-severity">
            <span class="sev-badge" :class="sevClass(a.severity)">{{ sevLabel(a.severity) }}</span>
            <component :is="sevIcon(a.severity)" :size="18" class="sev-icon" />
          </div>

          <div class="card-body">
            <div class="card-headline">
              <span class="card-host">{{ a.hostName }}</span>
              <span class="card-sep">·</span>
              <span class="card-agent">{{ a.agentId }}</span>
              <span class="card-time">{{ formatTime(a.detectedAt) }}</span>
            </div>

            <div class="card-description">{{ a.description }}</div>

            <div class="card-meta">
              <span v-for="(val, key) in a.affectedMetrics" :key="key" class="metric-tag">
                {{ metricLabel(String(key)) }}: <strong>{{ Number(val).toFixed(1) }}</strong>
              </span>
              <div class="conf-wrap">
                <span class="conf-label">Confidence</span>
                <div class="conf-bar">
                  <div class="conf-fill" :class="confClass(a.confidence)"
                    :style="{ width: (a.confidence * 100) + '%' }" />
                </div>
                <span class="conf-pct">{{ (a.confidence * 100).toFixed(0) }}%</span>
              </div>
            </div>

            <div class="card-insight">
              <div class="insight-row cause-row">
                <span class="insight-label">
                  <AlertTriangle :size="11" /> Likely cause
                </span>
                <span class="insight-text">{{ sevCause(a.severity, a.description) }}</span>
              </div>
              <div class="insight-row action-row">
                <span class="insight-label">
                  <Zap :size="11" /> Action
                </span>
                <span class="insight-text">{{ sevGuidance(a.severity, a.description) }}</span>
              </div>
            </div>
          </div>

          <div class="card-actions">
            <button class="btn btn-ghost ack-btn" @click="store.acknowledgeItem(a.id)">
              <CheckCircle :size="13" /> Acknowledge
            </button>
          </div>
        </div>

        <div v-if="activeAnomalies.length === 0" class="empty-state">
          <p>No active anomalies</p>
        </div>
      </div>
    </template>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  RefreshCw, CheckCircle, RotateCcw, ChevronDown,
  AlertOctagon, AlertTriangle, Info, Bell, Zap, Activity, GitBranch
} from 'lucide-vue-next'
import { formatDistanceToNow } from 'date-fns'
import { useMonitoringStore } from '@/stores/monitoring'
import type { SchemaResolutionEvent } from '@/types'

const store = useMonitoringStore()
const activeTab = ref<'schema' | 'anomalies'>('schema')
const showAcked = ref(false)

const selectedSeverity = ref('')
const selectedAgent = ref('')
const windowHours = ref(1)
const schemaAgent = ref('')
const schemaTier = ref('')

const criticalSchemaCount = computed(() =>
  store.schemaEvents.filter(e =>
    !store.isAcknowledged(e.id) && (!e.wasResolved || e.confidence < 0.60)
  ).length
)

const acknowledgedItems = computed(() => {
  const allItems = [
    ...store.schemaEvents.map(e => ({ ...e, _type: 'schema' as const })),
    ...[...store.anomalies, ...store.liveAnomalies].map(a => ({
      id: a.id, agentId: a.agentId, hostName: a.hostName,
      detectedAt: a.detectedAt, description: a.description, _type: 'anomaly' as const,
      rawAttribute: '', resolvedAttribute: null, wasResolved: false,
      confidence: a.confidence, resolutionTier: '', detectedAt2: a.detectedAt
    }))
  ].filter(i => store.isAcknowledged(i.id))
  return allItems
})

function ackedLabel(item: any) {
  if (item._type === 'schema')
    return item.wasResolved
      ? `"${item.rawAttribute}" → "${item.resolvedAttribute}"`
      : `"${item.rawAttribute}" — unresolved`
  return item.description ?? 'System anomaly'
}

const activeSchemaEvents = computed(() =>
  store.schemaEvents.filter(e => {
    if (store.isAcknowledged(e.id)) return false
    if (schemaAgent.value && e.agentId !== schemaAgent.value) return false
    if (schemaTier.value && e.resolutionTier !== schemaTier.value) return false
    return true
  })
)

const allAnomalies = computed(() => [
  ...store.liveAnomalies,
  ...store.anomalies
])

const activeAnomalies = computed(() =>
  allAnomalies.value.filter(a => {
    if (store.isAcknowledged(a.id)) return false
    if (selectedSeverity.value && sevLabel(a.severity) !== selectedSeverity.value) return false
    if (selectedAgent.value && a.agentId !== selectedAgent.value) return false
    return true
  })
)

function schemaClass(evt: SchemaResolutionEvent) {
  if (!evt.wasResolved) return 'critical'
  if (evt.confidence < 0.60) return 'critical'
  if (evt.confidence < 0.75) return 'high'
  if (evt.confidence < 0.90) return 'medium'
  return 'low'
}

function schemaLabel(evt: SchemaResolutionEvent) {
  if (!evt.wasResolved) return 'Critical'
  if (evt.confidence < 0.60) return 'Critical'
  if (evt.confidence < 0.75) return 'High'
  if (evt.confidence < 0.90) return 'Medium'
  return 'Low'
}

function schemaIcon(evt: SchemaResolutionEvent) {
  const c = schemaClass(evt)
  if (c === 'critical') return AlertOctagon
  if (c === 'high') return AlertTriangle
  if (c === 'medium') return Bell
  return Info
}

function schemaCause(evt: SchemaResolutionEvent) {
  if (!evt.wasResolved)
    return `The attribute "${evt.rawAttribute}" was received but could not be mapped to any known canonical attribute. Data from this field is not being captured.`
  if (evt.confidence < 0.75)
    return `"${evt.rawAttribute}" was tentatively mapped to "${evt.resolvedAttribute}" but with low confidence — the vendor may have changed this field name in a recent firmware or software update.`
  return `"${evt.rawAttribute}" differs from the expected canonical name "${evt.resolvedAttribute}" — likely a naming convention difference between vendors or software versions.`
}

function schemaAction(evt: SchemaResolutionEvent) {
  if (!evt.wasResolved)
    return `Go to Schema Registry, find "${evt.rawAttribute}" in the unknown queue, and manually map it to the correct canonical attribute. Once confirmed, this mapping is permanent.`
  if (evt.confidence < 0.75)
    return `Verify in Schema Registry that "${evt.rawAttribute}" should map to "${evt.resolvedAttribute}". If correct, confirm the mapping to raise confidence permanently.`
  return `No immediate action required. The mapping was resolved automatically. Confirm in Schema Registry if you want to lock it in.`
}

const schemaGuide = [
  { level: 'Critical', headline: 'Unresolved or <60% confidence', action: 'Data may be missing. Manual review required immediately.' },
  { level: 'High', headline: '60–74% confidence', action: 'Likely resolved but uncertain. Verify and confirm.' },
  { level: 'Medium', headline: '75–89% confidence', action: 'Probably correct. Confirm when convenient.' },
  { level: 'Low', headline: '90%+ confidence', action: 'Auto-resolved confidently. No action needed.' },
]

const anomalyGuide = [
  { level: 'Critical', headline: 'Immediate action required', action: 'System at risk — act now.' },
  { level: 'High', headline: 'Investigate within the hour', action: 'Significant deviation detected.' },
  { level: 'Medium', headline: 'Monitor closely', action: 'Unusual pattern — keep an eye on it.' },
  { level: 'Low', headline: 'Informational', action: 'Minor deviation, no immediate action.' },
]

const SEV_MAP: Record<string | number, string> = {
  0: 'low', 1: 'medium', 2: 'high', 3: 'critical',
  Low: 'low', Medium: 'medium', High: 'high', Critical: 'critical'
}
const SEV_LABEL: Record<string | number, string> = {
  0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical',
  Low: 'Low', Medium: 'Medium', High: 'High', Critical: 'Critical'
}
function sevClass(s: any) { return SEV_MAP[s] ?? 'low' }
function sevLabel(s: any) { return SEV_LABEL[s] ?? String(s) }
function sevIcon(s: any) {
  const c = sevClass(s)
  if (c === 'critical') return AlertOctagon
  if (c === 'high') return AlertTriangle
  if (c === 'medium') return Bell
  return Info
}

const METRIC_LABELS: Record<string, string> = {
  cpu_percent: 'CPU', memory_percent: 'Memory',
  disk_write_mbps: 'Disk Write', disk_read_mbps: 'Disk Read',
  error_rate: 'Error Rate', p99_latency_ms: 'P99 Latency',
  network_in_mbps: 'Net In', network_out_mbps: 'Net Out',
  requests_per_second: 'Req/s',
}
function metricLabel(key: string) { return METRIC_LABELS[key] ?? key }

function confClass(c: number) {
  if (c >= 0.9) return 'fill-danger'
  if (c >= 0.7) return 'fill-warning'
  return 'fill-accent'
}

function sevCause(s: any, description: string) {
  const desc = description.toLowerCase()
  if (desc.includes('cpu')) return 'Excessive CPU consumption — possible runaway process, traffic spike, or tight loop.'
  if (desc.includes('memory')) return 'Memory pressure — possible leak, large dataset in RAM, or too many connections.'
  if (desc.includes('error')) return 'Elevated error rate — possible bad deployment, downstream outage, or DB connectivity issue.'
  if (desc.includes('latency')) return 'Response time degraded — slow DB query, network congestion, or overloaded dependency.'
  if (desc.includes('disk')) return 'Disk write spike — logging storm, large file writes, or DB checkpointing.'
  return 'Unexpected deviation from normal baseline detected by the anomaly model.'
}

function sevGuidance(s: any, description: string) {
  const cls = sevClass(s)
  const desc = description.toLowerCase()
  if (desc.includes('cpu'))
    return cls === 'critical'
      ? 'Run top or Task Manager. Kill offending process or scale immediately.'
      : 'Monitor for 5–10 min. Check scheduled jobs or traffic increase.'
  if (desc.includes('memory'))
    return cls === 'critical'
      ? 'OOM risk imminent. Restart service, then investigate heap dumps.'
      : 'Check in-memory caches and recent code changes for leaks.'
  if (desc.includes('error'))
    return cls === 'critical'
      ? 'Check logs immediately. Roll back last deployment if errors started after a release.'
      : 'Review error logs for patterns. Check upstream dependencies.'
  if (desc.includes('latency'))
    return 'Profile slow endpoints. Look for N+1 queries or missing DB indexes.'
  return cls === 'critical'
    ? 'Investigate immediately. Check logs, recent deployments, and dependencies.'
    : 'Review system logs and monitor for escalation.'
}

function formatTime(ts: string) {
  try { return formatDistanceToNow(new Date(ts), { addSuffix: true }) }
  catch { return ts }
}

function refresh() { store.fetchAnomalies(windowHours.value * 60) }
onMounted(refresh)
</script>

<style scoped>
.anomalies-view {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.tabs {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--border);
  padding-bottom: 0;
}

.tab {
  display: flex;
  align-items: center;
  gap: 7px;
  padding: 9px 16px;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  font-family: var(--sans);
  font-size: 13px;
  font-weight: 500;
  color: var(--text-dim);
  cursor: pointer;
  transition: all var(--transition);
  margin-bottom: -1px;
}

.tab:hover {
  color: var(--text-muted);
}

.tab.active {
  color: var(--text-primary);
  border-bottom-color: var(--accent);
}

.tab-count {
  padding: 1px 6px;
  border-radius: 10px;
  font-family: var(--mono);
  font-size: 10px;
  font-weight: 700;
  background: var(--bg-hover);
  color: var(--text-dim);
}

.tab-count.count-danger {
  background: rgba(255, 59, 107, 0.15);
  color: var(--danger);
}

.acked-panel {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}

.acked-panel-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 14px;
  font-size: 12px;
  color: var(--success);
  font-weight: 500;
}

.acked-collapse {
  margin-left: auto;
  background: none;
  border: none;
  cursor: pointer;
  color: var(--text-dim);
  display: flex;
  transition: transform var(--transition);
}

.acked-collapse .rotated {
  transform: rotate(180deg);
}

.acked-list {
  border-top: 1px solid var(--border);
}

.acked-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 14px;
  border-bottom: 1px solid color-mix(in srgb, var(--border) 50%, transparent);
  font-size: 12px;
}

.acked-row:last-child {
  border-bottom: none;
}

.acked-icon {
  color: var(--success);
  flex-shrink: 0;
}

.acked-label {
  flex: 1;
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-muted);
}

.acked-time {
  font-size: 10px;
  color: var(--text-dim);
  flex-shrink: 0;
}

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

.undo-btn:hover {
  border-color: var(--text-muted);
  color: var(--text-muted);
}

.severity-guide {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 12px 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.guide-title {
  font-family: var(--mono);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-dim);
}

.guide-items {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 10px;
}

.guide-item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}

.guide-text {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.guide-headline {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

.guide-action {
  font-size: 11px;
  color: var(--text-dim);
  line-height: 1.4;
}

.filter-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.filter-count {
  margin-left: auto;
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-dim);
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
  width: fit-content;
}

.live-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--accent);
  animation: pulse 1.5s infinite;
}

.card-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

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

.anomaly-card:hover {
  background: var(--bg-hover);
}

.card-critical {
  border-left-color: var(--danger);
}

.card-high {
  border-left-color: var(--warning);
}

.card-medium {
  border-left-color: var(--accent);
}

.card-low {
  border-left-color: var(--border);
}

.card-severity {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
  width: 72px;
}

.sev-icon {
  color: var(--text-dim);
}

.card-critical .sev-icon {
  color: var(--danger);
}

.card-high .sev-icon {
  color: var(--warning);
}

.card-medium .sev-icon {
  color: var(--accent);
}

.card-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.card-headline {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.card-host {
  font-family: var(--mono);
  font-size: 12px;
  font-weight: 700;
  color: var(--text-primary);
}

.card-agent {
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-muted);
}

.card-sep {
  color: var(--text-dim);
}

.card-time {
  font-size: 11px;
  color: var(--text-dim);
  margin-left: auto;
}

.attr-change {
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: var(--mono);
  font-size: 13px;
}

.attr-raw {
  color: var(--text-primary);
  font-weight: 700;
}

.attr-arrow {
  color: var(--text-dim);
}

.attr-resolved {
  color: var(--success);
}

.attr-unresolved {
  color: var(--danger);
}

.card-description {
  font-size: 13px;
  color: var(--text-muted);
}

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

.metric-tag strong {
  color: var(--text-primary);
}

.conf-wrap {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-left: auto;
}

.conf-label {
  font-size: 10px;
  color: var(--text-dim);
  font-family: var(--mono);
}

.conf-bar {
  width: 60px;
  height: 4px;
  background: var(--bg-surface);
  border-radius: 2px;
  overflow: hidden;
}

.conf-fill {
  height: 100%;
  border-radius: 2px;
  transition: width 400ms ease;
}

.fill-danger {
  background: var(--danger);
}

.fill-warning {
  background: var(--warning);
}

.fill-accent {
  background: var(--accent);
}

.conf-pct {
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-muted);
  width: 28px;
}

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

.cause-row .insight-label {
  color: var(--warning);
}

.action-row .insight-label {
  color: var(--accent);
}

.insight-text {
  color: var(--text-muted);
}

.card-actions {
  flex-shrink: 0;
  padding-top: 2px;
}

.ack-btn {
  font-size: 11px;
  padding: 5px 12px;
}

.loading-rows {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.slide-enter-active {
  transition: all 200ms ease;
}

.slide-leave-active {
  transition: all 150ms ease;
}

.slide-enter-from,
.slide-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

@keyframes pulse {

  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.4;
  }
}

@media (max-width: 900px) {
  .guide-items {
    grid-template-columns: repeat(2, 1fr);
  }

  .anomaly-card {
    flex-direction: column;
  }

  .card-severity {
    flex-direction: row;
    width: auto;
  }
}
</style>