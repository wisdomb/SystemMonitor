<template>
  <div class="infra-status">
    <div v-if="!store.infraStatus" class="skeleton" style="height:80px" />
    <div v-else class="infra-grid">
      <div class="infra-stat">
        <span class="infra-label">Metric Queue</span>
        <span class="infra-value" :class="queueClass(store.infraStatus.metricQueueDepth)">
          {{ store.infraStatus.metricQueueDepth.toLocaleString() }}
        </span>
      </div>
      <div class="infra-stat">
        <span class="infra-label">Log Queue</span>
        <span class="infra-value" :class="queueClass(store.infraStatus.logQueueDepth)">
          {{ store.infraStatus.logQueueDepth.toLocaleString() }}
        </span>
      </div>
      <div class="infra-stat">
        <span class="infra-label">Workers</span>
        <span class="infra-value ok">{{ store.infraStatus.workerCount }}</span>
      </div>
      <div class="infra-stat">
        <span class="infra-label">Proc Lag</span>
        <span class="infra-value" :class="lagClass(store.infraStatus.processingDelayMs)">
          {{ store.infraStatus.processingDelayMs }}ms
        </span>
      </div>
      <div class="infra-stat">
        <span class="infra-label">Cosmos RU/s</span>
        <span class="infra-value ok">
          {{ store.infraStatus.cosmosRequestUnits.toFixed(0) }}
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useMonitoringStore } from '@/stores/monitoring'
import { useIntervalFn } from '@vueuse/core'

const store = useMonitoringStore()
useIntervalFn(() => store.fetchInfrastructure(), 15_000)

function queueClass(depth: number) {
  if (depth > 10_000) return 'bad'
  if (depth > 1_000)  return 'warn'
  return 'ok'
}

function lagClass(ms: number) {
  if (ms > 5000) return 'bad'
  if (ms > 1000) return 'warn'
  return 'ok'
}
</script>

<style scoped>
.infra-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 12px;
}

.infra-stat {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px;
  background: var(--bg-surface);
  border-radius: var(--radius);
  border: 1px solid var(--border);
}

.infra-label {
  font-size: 10px;
  font-family: var(--mono);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--text-dim);
}

.infra-value {
  font-family: var(--mono);
  font-size: 18px;
  font-weight: 700;
}

.ok   { color: var(--success); }
.warn { color: var(--warning); }
.bad  { color: var(--danger); }

@media (max-width: 900px) {
  .infra-grid { grid-template-columns: repeat(3, 1fr); }
}
</style>
