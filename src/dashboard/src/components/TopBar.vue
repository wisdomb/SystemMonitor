<template>
  <header class="topbar">
    <div class="topbar-left">
      <h1 class="page-title">{{ currentPageTitle }}</h1>
    </div>

    <div class="topbar-right">
      <div class="stat-pill">
        <Activity :size="13" />
        <span>{{ store.ingestionRate.toLocaleString() }} ev/min</span>
      </div>

      <div class="health-chip" :class="healthClass">
        <Gauge :size="13" />
        <span>Health {{ store.overallHealth }}%</span>
      </div>

      <div class="time-display">{{ currentTime }}</div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { Activity, Gauge } from 'lucide-vue-next'
import { useMonitoringStore } from '@/stores/monitoring'
import { useIntervalFn } from '@vueuse/core'
import { format } from 'date-fns'

const store = useMonitoringStore()
const route = useRoute()

const titleMap: Record<string, string> = {
  '/': 'Overview',
  '/anomalies': 'Anomaly Detection',
  '/metrics': 'Metrics Explorer',
  '/agents': 'Agent Fleet',
  '/logs': 'Log Stream',
  '/infra': 'Infrastructure',
  '/schema': 'Schema Registry',
  '/training': 'Training Data',
}

const currentPageTitle = computed(() => titleMap[route.path] || 'Monitor')

const healthClass = computed(() => {
  const s = store.overallHealth
  if (s >= 80) return 'health-good'
  if (s >= 50) return 'health-warn'
  return 'health-bad'
})

const currentTime = ref(format(new Date(), 'HH:mm:ss'))
useIntervalFn(() => {
  currentTime.value = format(new Date(), 'HH:mm:ss')
}, 1000)
</script>

<style scoped>
.topbar {
  height: 52px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  border-bottom: 1px solid var(--border);
  background: var(--bg-panel);
  flex-shrink: 0;
}

.page-title {
  font-family: var(--mono);
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.05em;
  color: var(--text-primary);
}

.topbar-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.stat-pill {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 10px;
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: 20px;
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-muted);
}

.health-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 10px;
  border-radius: 20px;
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
  border: 1px solid;
}

.health-good {
  background: color-mix(in srgb, var(--success) 12%, transparent);
  color: var(--success);
  border-color: color-mix(in srgb, var(--success) 30%, transparent);
}

.health-warn {
  background: color-mix(in srgb, var(--warning) 12%, transparent);
  color: var(--warning);
  border-color: color-mix(in srgb, var(--warning) 30%, transparent);
}

.health-bad {
  background: color-mix(in srgb, var(--danger) 12%, transparent);
  color: var(--danger);
  border-color: color-mix(in srgb, var(--danger) 30%, transparent);
}

.time-display {
  font-family: var(--mono);
  font-size: 12px;
  color: var(--text-muted);
  min-width: 60px;
  text-align: right;
}
</style>