<template>
  <div class="anomaly-feed">
    <div class="feed-list">
      <div v-for="anomaly in displayList" :key="anomaly.id" class="feed-row"
        :class="`sev-${sevClass(anomaly.severity)}`">
        <span class="sev-dot" />
        <div class="feed-info">
          <span class="feed-host">{{ anomaly.hostName }}</span>
          <span class="feed-desc">{{ anomaly.description }}</span>
        </div>
        <div class="feed-meta">
          <span class="feed-sev">{{ sevLabel(anomaly.severity) }}</span>
          <span class="feed-time">{{ relativeTime(anomaly.detectedAt) }}</span>
        </div>
      </div>
    </div>
    <div v-if="displayList.length === 0" class="empty-state">
      No anomalies detected
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { formatDistanceToNow } from 'date-fns'
import type { AnomalyResult } from '@/types'

const props = withDefaults(defineProps<{
  anomalies: AnomalyResult[]
  max?: number
}>(), { max: 5 })

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

const displayList = computed(() =>
  [...props.anomalies]
    .sort((a, b) =>
      new Date(b.detectedAt).getTime() - new Date(a.detectedAt).getTime()
    )
    .slice(0, props.max)
)

function relativeTime(ts: string) {
  try { return formatDistanceToNow(new Date(ts), { addSuffix: true }) }
  catch { return ts }
}
</script>

<style scoped>
.feed-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.feed-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 6px;
  background: var(--bg-surface);
  border-left: 2px solid transparent;
}

.sev-critical {
  border-left-color: var(--danger);
}

.sev-high {
  border-left-color: var(--warning);
}

.sev-medium {
  border-left-color: var(--accent);
}

.sev-low {
  border-left-color: var(--text-dim);
}

.sev-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.sev-critical .sev-dot {
  background: var(--danger);
  box-shadow: 0 0 6px var(--danger);
}

.sev-high .sev-dot {
  background: var(--warning);
}

.sev-medium .sev-dot {
  background: var(--accent);
}

.sev-low .sev-dot {
  background: var(--text-dim);
}

.feed-info {
  flex: 1;
  min-width: 0;
}

.feed-host {
  display: block;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

.feed-desc {
  display: block;
  font-size: 11px;
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.feed-meta {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
  flex-shrink: 0;
}

.feed-sev {
  font-family: var(--mono);
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
}

.sev-critical .feed-sev {
  color: var(--danger);
}

.sev-high .feed-sev {
  color: var(--warning);
}

.sev-medium .feed-sev {
  color: var(--accent);
}

.sev-low .feed-sev {
  color: var(--text-dim);
}

.feed-time {
  font-size: 10px;
  color: var(--text-dim);
}

.empty-state {
  padding: 24px;
  text-align: center;
  font-size: 12px;
  color: var(--text-dim);
}
</style>