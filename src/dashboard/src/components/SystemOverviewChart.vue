<template>
  <div class="chart-wrap">
    <div class="chart-controls">
      <div class="metric-pills">
        <button v-for="m in metrics" :key="m.key" class="metric-pill"
          :class="{ active: selectedMetrics.includes(m.key) }" @click="toggleMetric(m.key)">
          <span class="pill-dot" :style="{ background: m.color }" />
          {{ m.label }}
        </button>
      </div>

      <select v-model="windowMinutes" class="window-select" @change="fetchChartData">
        <option :value="15">15 min</option>
        <option :value="30">30 min</option>
        <option :value="60">1 hour</option>
      </select>
    </div>

    <div v-if="loading" class="skeleton" style="height: 220px; margin-top: 12px;" />

    <div v-else-if="!hasData" class="chart-empty">
      <p>Select metric from the top row</p>
      <p class="hint">Agent flushes every 30 seconds</p>
    </div>

    <Line v-else :data="chartData" :options="chartOptions" style="max-height: 220px; margin-top: 12px;" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { Line } from 'vue-chartjs'
import {
  Chart as ChartJS, CategoryScale, LinearScale,
  PointElement, LineElement, Tooltip, Filler, Legend
} from 'chart.js'
import { useMonitoringStore } from '@/stores/monitoring'
import { useIntervalFn } from '@vueuse/core'
import { format } from 'date-fns'
import type { TimeSeriesPoint } from '@/types'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip, Filler, Legend)

const props = defineProps<{ agentId: string | null }>()

const store = useMonitoringStore()
const loading = ref(true)
const windowMinutes = ref(30)
const selectedMetrics = ref(['cpu_percent', 'memory_percent'])

const metrics = [
  { key: 'cpu_percent', label: 'CPU %', color: '#a0aabb' },
  { key: 'memory_percent', label: 'Memory %', color: '#00e5a0' },
  { key: 'disk_read_mbps', label: 'Disk Read', color: '#ffb830' },
  { key: 'disk_write_mbps', label: 'Disk Write', color: '#ff8c42' },
  { key: 'network_in_mbps', label: 'Net In', color: '#7c83fd' },
  { key: 'network_out_mbps', label: 'Net Out', color: '#c084fc' },
  { key: 'error_rate', label: 'Error Rate', color: '#ff3b6b' },
  { key: 'p99_latency_ms', label: 'P99 Latency', color: '#fb923c' },
  { key: 'requests_per_second', label: 'Req/s', color: '#34d399' },
]

const seriesData = ref<Record<string, TimeSeriesPoint[]>>({})

function toggleMetric(key: string) {
  if (selectedMetrics.value.includes(key))
    selectedMetrics.value = selectedMetrics.value.filter(k => k !== key)
  else
    selectedMetrics.value = [...selectedMetrics.value, key]
}

async function fetchChartData() {
  const agentId = props.agentId ?? store.agentList[0]?.agentId ?? null
  if (!agentId) { loading.value = false; return }

  loading.value = true
  try {
    const results = await Promise.allSettled(
      metrics.map(m =>
        store.fetchTimeSeries(agentId, m.key, windowMinutes.value)
          .then(data => ({ key: m.key, data }))
      )
    )
    const fresh: Record<string, TimeSeriesPoint[]> = {}
    for (const r of results)
      if (r.status === 'fulfilled') fresh[r.value.key] = r.value.data
    seriesData.value = fresh
  } catch { }

  loading.value = false
}

const hasData = computed(() =>
  selectedMetrics.value.some(k =>
    (seriesData.value[k] ?? []).length > 0
  )
)

const chartData = computed(() => {
  const baseKey = selectedMetrics.value.find(k => (seriesData.value[k]?.length ?? 0) > 0)
  const labels = baseKey
    ? (seriesData.value[baseKey] ?? []).map(p => format(new Date(p.timestamp), 'HH:mm'))
    : []

  const datasets = selectedMetrics.value
    .filter(k => (seriesData.value[k]?.length ?? 0) > 0)
    .map(key => {
      const meta = metrics.find(m => m.key === key)!
      return {
        label: meta.label,
        data: seriesData.value[key].map(p => p.value),
        borderColor: meta.color,
        backgroundColor: meta.color + '12',
        borderWidth: 2,
        pointRadius: 0,
        fill: selectedMetrics.value.length === 1,
        tension: 0.35,
      }
    })

  return { labels, datasets }
})

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  plugins: {
    legend: {
      display: false,
    },
    tooltip: {
      backgroundColor: '#0d1525',
      borderColor: '#1c2a42',
      borderWidth: 1,
      titleColor: '#6b7fa3',
      bodyColor: '#e8edf5',
      padding: 10,
      titleFont: { family: 'Space Mono', size: 10 },
      bodyFont: { family: 'DM Sans', size: 12 },
    },
  },
  scales: {
    x: {
      grid: { color: '#1c2a42', drawBorder: false },
      ticks: { color: '#3d4f6e', font: { family: 'Space Mono', size: 9 }, maxTicksLimit: 8 },
    },
    y: {
      grid: { color: '#1c2a42', drawBorder: false },
      ticks: { color: '#3d4f6e', font: { family: 'Space Mono', size: 9 } },
    },
  },
}

onMounted(fetchChartData)
useIntervalFn(fetchChartData, 30_000)
watch(() => props.agentId, fetchChartData)
watch(() => store.agentList.length, (n, o) => { if (o === 0 && n > 0) fetchChartData() })
</script>

<style scoped>
.chart-wrap {
  display: flex;
  flex-direction: column;
}

.chart-controls {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  flex-wrap: wrap;
}

.metric-pills {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.metric-pill {
  display: flex;
  align-items: center;
  gap: 5px;
  padding: 3px 10px;
  border-radius: 12px;
  font-family: var(--mono);
  font-size: 10px;
  font-weight: 600;
  border: 1px solid var(--border);
  background: var(--bg-surface);
  color: var(--text-dim);
  cursor: pointer;
  transition: all var(--transition);
}

.metric-pill:hover {
  color: var(--text-muted);
  border-color: var(--text-dim);
}

.metric-pill.active {
  color: var(--text-primary);
  background: var(--bg-hover);
  border-color: var(--text-muted);
}

.pill-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.window-select {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-muted);
  font-family: var(--mono);
  font-size: 10px;
  padding: 4px 8px;
  outline: none;
  flex-shrink: 0;
}

.window-select:focus {
  border-color: var(--accent);
}

.chart-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 220px;
  gap: 6px;
  color: var(--text-dim);
  font-size: 12px;
  margin-top: 12px;
}

.hint {
  font-size: 11px;
  opacity: 0.6;
}
</style>