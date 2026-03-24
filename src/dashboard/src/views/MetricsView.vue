<template>
  <div class="metrics-view">
    <div class="controls-row">
      <select v-model="selectedAgent" class="filter-select" @change="onAgentChange">
        <option value="">— Select Agent —</option>
        <option v-for="a in agents" :key="a" :value="a">{{ a }}</option>
      </select>
      <select v-model="selectedMetric" class="filter-select" :disabled="!selectedAgent">
        <option value="">— Select Metric —</option>
        <option v-for="m in agentMetrics" :key="m.key" :value="m.key">
          {{ m.label }}
        </option>
      </select>
      <select v-model="windowMinutes" class="filter-select">
        <option :value="15">15 min</option>
        <option :value="30">30 min</option>
        <option :value="60">1 hour</option>
        <option :value="360">6 hours</option>
        <option :value="1440">24 hours</option>
      </select>

      <button class="btn btn-primary" :disabled="!selectedAgent || !selectedMetric" @click="loadChart">
        <BarChart2 :size="13" />
        Load
      </button>

      <label class="auto-refresh-toggle">
        <input type="checkbox" v-model="autoRefresh" />
        Auto-refresh
      </label>
    </div>

    <div v-if="agents.length > 1" class="agent-pills">
      <button v-for="a in agents" :key="a" class="agent-pill" :class="{ active: selectedAgent === a }"
        @click="selectedAgent = a; onAgentChange()">
        {{ a }}
      </button>
    </div>

    <div class="chart-panel">
      <div class="chart-header">
        <div class="chart-title-group">
          <span class="chart-agent">{{ selectedAgent || 'No agent selected' }}</span>
          <span class="chart-sep">/</span>
          <span class="chart-metric">{{ metricLabel }}</span>
        </div>
        <div class="chart-meta">
          <span v-if="series.length" class="data-points">
            {{ series.length }} points
          </span>
          <span v-if="lastLoaded" class="last-loaded">
            Updated {{ lastLoaded }}
          </span>
        </div>
      </div>

      <div class="chart-body">
        <div v-if="loading" class="chart-skeleton">
          <div class="skeleton" style="height: 280px; width: 100%;" />
        </div>

        <div v-else-if="!selectedAgent || !selectedMetric" class="empty-state">
          <div class="empty-icon">📊</div>
          <p>Select an agent and metric above, then click <strong>Load</strong></p>
          <p v-if="agents.length === 0" style="color: var(--warning); margin-top: 8px;">
            No agents reporting yet — start an agent first
          </p>
        </div>

        <div v-else-if="series.length === 0 && !loading" class="empty-state">
          <div class="empty-icon">🔍</div>
          <p>No data for <strong>{{ selectedMetric }}</strong> in the last {{ windowMinutes }} minutes</p>
          <p>The agent may not have flushed yet — it sends every 30 seconds</p>
        </div>

        <Line v-else :data="chartData" :options="chartOptions" style="max-height: 320px" />
      </div>
    </div>

    <div v-if="series.length" class="stats-row">
      <div class="stat-box">
        <span class="stat-label">Min</span>
        <span class="stat-val">{{ stats.min.toFixed(2) }}</span>
      </div>
      <div class="stat-box">
        <span class="stat-label">Max</span>
        <span class="stat-val" :class="{ 'text-danger': isHighMax }">
          {{ stats.max.toFixed(2) }}
        </span>
      </div>
      <div class="stat-box">
        <span class="stat-label">Avg</span>
        <span class="stat-val">{{ stats.avg.toFixed(2) }}</span>
      </div>
      <div class="stat-box">
        <span class="stat-label">P95</span>
        <span class="stat-val">{{ stats.p95.toFixed(2) }}</span>
      </div>
      <div class="stat-box">
        <span class="stat-label">Std Dev</span>
        <span class="stat-val">{{ stats.stddev.toFixed(2) }}</span>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Line } from 'vue-chartjs'
import {
  Chart as ChartJS, CategoryScale, LinearScale,
  PointElement, LineElement, Tooltip, Filler
} from 'chart.js'
import { BarChart2 } from 'lucide-vue-next'
import { format, formatDistanceToNow } from 'date-fns'
import { useMonitoringStore } from '@/stores/monitoring'
import api from '@/utils/api'
import type { TimeSeriesPoint } from '@/types'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip, Filler)

const store = useMonitoringStore()
const selectedAgent = ref('')
const selectedMetric = ref('')
const windowMinutes = ref(30)
const series = ref<TimeSeriesPoint[]>([])
const loading = ref(false)
const autoRefresh = ref(true)
const lastLoaded = ref('')
const agents = ref<string[]>([])
const rawMetrics = ref<string[]>([])

const metricLabels: Record<string, string> = {
  cpu_percent: 'CPU %',
  memory_percent: 'Memory %',
  disk_percent: 'Disk %',
  disk_read_mbps: 'Disk Read (Mbps)',
  disk_write_mbps: 'Disk Write (Mbps)',
  network_in_mbps: 'Network In (Mbps)',
  network_out_mbps: 'Network Out (Mbps)',
  requests_per_second: 'Requests/s',
  error_rate: 'Error Rate',
  p99_latency_ms: 'P99 Latency (ms)',
}

const agentMetrics = computed(() =>
  rawMetrics.value.map(key => ({
    key,
    label: metricLabels[key] ?? key
  })).sort((a, b) => a.label.localeCompare(b.label))
)

const metricLabel = computed(() =>
  (metricLabels[selectedMetric.value] ?? selectedMetric.value) || 'No metric selected'
)

async function loadAgents() {
  const storeAgents = store.agentList.map(a => a.agentId)
  if (storeAgents.length > 0) {
    agents.value = storeAgents
    if (agents.value.length >= 1 && !selectedAgent.value) {
      selectedAgent.value = agents.value[0]
      await loadMetricsForAgent(agents.value[0])
    }
  }

  try {
    const res = await api.get<string[]>('/api/v1/analytics/agents')
    if (res.data?.length) {
      agents.value = res.data
      if (!selectedAgent.value && agents.value.length >= 1) {
        selectedAgent.value = agents.value[0]
        await loadMetricsForAgent(agents.value[0])
      }
    }
  } catch { }
}

async function loadMetricsForAgent(agentId: string) {
  if (!agentId) { rawMetrics.value = []; return }
  try {
    const res = await api.get<string[]>(`/api/v1/analytics/metrics/${agentId}`)
    rawMetrics.value = res.data ?? []

    if (rawMetrics.value.length > 0 && !selectedMetric.value) {
      selectedMetric.value = rawMetrics.value.includes('cpu_percent')
        ? 'cpu_percent'
        : rawMetrics.value[0]
    }
  } catch { rawMetrics.value = [] }
}

async function onAgentChange() {
  selectedMetric.value = ''
  series.value = []
  await loadMetricsForAgent(selectedAgent.value)
}

async function loadChart() {
  if (!selectedAgent.value || !selectedMetric.value) return
  loading.value = true
  try {
    series.value = await store.fetchTimeSeries(
      selectedAgent.value, selectedMetric.value, windowMinutes.value)
    lastLoaded.value = formatDistanceToNow(new Date(), { addSuffix: true })
  } finally {
    loading.value = false
  }
}

let refreshTimer: ReturnType<typeof setInterval> | null = null
watch(autoRefresh, (enabled) => {
  if (refreshTimer) clearInterval(refreshTimer)
  if (enabled) refreshTimer = setInterval(() => {
    if (selectedAgent.value && selectedMetric.value) loadChart()
  }, 30_000)
}, { immediate: true })

watch(() => store.agentList.length, async (n, o) => {
  await loadAgents()
  if (n > o) loadChart()
})

onMounted(async () => {
  await loadAgents()
})

const chartData = computed(() => ({
  labels: series.value.map(p => format(new Date(p.timestamp), 'HH:mm:ss')),
  datasets: [{
    label: metricLabel.value,
    data: series.value.map(p => p.value),
    borderColor: '#a0aabb',
    backgroundColor: 'rgba(160,170,187,0.07)',
    borderWidth: 2,
    pointRadius: series.value.length < 60 ? 3 : 0,
    pointHoverRadius: 5,
    pointBackgroundColor: '#a0aabb',
    fill: true,
    tension: 0.35
  }]
}))

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  plugins: {
    legend: { display: false },
    tooltip: {
      backgroundColor: '#0d1525',
      borderColor: '#1c2a42',
      borderWidth: 1,
      titleColor: '#6b7fa3',
      bodyColor: '#e8edf5',
      padding: 12,
      titleFont: { family: 'Space Mono', size: 10 },
      bodyFont: { family: 'DM Sans', size: 13 },
      callbacks: {
        label: (ctx: any) => ` ${ctx.parsed.y.toFixed(3)}`
      }
    }
  },
  scales: {
    x: {
      grid: { color: '#1c2a42', drawBorder: false },
      ticks: { color: '#3d4f6e', font: { family: 'Space Mono', size: 9 }, maxTicksLimit: 10 }
    },
    y: {
      grid: { color: '#1c2a42', drawBorder: false },
      ticks: { color: '#3d4f6e', font: { family: 'Space Mono', size: 9 } }
    }
  }
}

const stats = computed(() => {
  const vals = series.value.map(p => p.value).sort((a, b) => a - b)
  if (!vals.length) return { min: 0, max: 0, avg: 0, p95: 0, stddev: 0 }
  const avg = vals.reduce((a, b) => a + b, 0) / vals.length
  const stddev = Math.sqrt(vals.reduce((s, v) => s + (v - avg) ** 2, 0) / vals.length)
  const p95 = vals[Math.floor(vals.length * 0.95)] ?? vals[vals.length - 1]
  return { min: vals[0], max: vals[vals.length - 1], avg, p95, stddev }
})

const isHighMax = computed(() =>
  stats.value.max > 90 &&
  (selectedMetric.value.includes('percent') || selectedMetric.value.includes('rate'))
)
</script>

<style scoped>
.metrics-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.controls-row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.auto-refresh-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
  cursor: pointer;
  margin-left: auto;
}

.agent-pills {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.agent-pill {
  padding: 4px 12px;
  border-radius: 20px;
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 600;
  border: 1px solid var(--border);
  background: var(--bg-surface);
  color: var(--text-muted);
  cursor: pointer;
  transition: all var(--transition);
}

.agent-pill:hover {
  border-color: var(--accent);
  color: var(--accent);
}

.agent-pill.active {
  border-color: var(--accent);
  background: var(--accent-glow);
  color: var(--accent);
}

.chart-panel {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 18px;
  border-bottom: 1px solid var(--border);
}

.chart-title-group {
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: var(--mono);
  font-size: 12px;
}

.chart-agent {
  color: var(--text-muted);
}

.chart-sep {
  color: var(--text-dim);
}

.chart-metric {
  color: var(--accent);
}

.chart-meta {
  display: flex;
  align-items: center;
  gap: 12px;
}

.data-points {
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-dim);
}

.last-loaded {
  font-size: 11px;
  color: var(--text-dim);
}

.chart-body {
  padding: 20px;
  min-height: 340px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.chart-skeleton {
  width: 100%;
}

.empty-state {
  text-align: center;
  color: var(--text-dim);
  font-size: 13px;
  line-height: 2;
}

.empty-icon {
  font-size: 32px;
  margin-bottom: 12px;
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 10px;
}

.stat-box {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.stat-label {
  font-size: 10px;
  font-family: var(--mono);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--text-dim);
}

.stat-val {
  font-family: var(--mono);
  font-size: 20px;
  font-weight: 700;
  color: var(--accent);
}

.text-danger {
  color: var(--danger) !important;
}
</style>