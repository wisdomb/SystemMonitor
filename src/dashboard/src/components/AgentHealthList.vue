<template>
  <div class="agent-list">
    <div v-for="agent in store.agentList" :key="agent.agentId" class="agent-row"
      :class="{ highlighted: highlight === agent.agentId }"
      @click="emit('select', agent.agentId === highlight ? '' : agent.agentId)">
      <div class="agent-id">
        <span class="status-dot" :class="statusClass(agent.score)" />
        <span class="agent-name">{{ agent.agentId }}</span>
      </div>

      <div class="score-bar-wrap">
        <div class="score-bar">
          <div class="score-fill" :class="scoreColorClass(agent.score)" :style="{ width: agent.score + '%' }" />
        </div>
        <span class="score-label">{{ Math.round(agent.score) }}</span>
      </div>

      <!-- Click hint -->
      <span class="row-hint">{{ highlight === agent.agentId ? 'click to deselect' : 'click to focus' }}</span>
    </div>

    <div v-if="store.agentList.length === 0" class="no-agents">
      No active agents — start an agent to see data
    </div>
  </div>
</template>

<script setup lang="ts">
import { useMonitoringStore } from '@/stores/monitoring'

defineProps<{ highlight?: string }>()
const emit = defineEmits<{ select: [agentId: string] }>()
const store = useMonitoringStore()

function statusClass(score: number) {
  if (score >= 80) return 'ok'
  if (score >= 50) return 'warn'
  return 'err'
}
function scoreColorClass(score: number) {
  if (score >= 80) return 'fill-success'
  if (score >= 50) return 'fill-warning'
  return 'fill-danger'
}
</script>

<style scoped>
.agent-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.agent-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 10px;
  border-radius: var(--radius-sm);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all var(--transition);
}

.agent-row:hover {
  background: var(--bg-hover);
  border-color: var(--border);
}

.agent-row.highlighted {
  background: rgba(160, 170, 187, 0.06);
  border-color: rgba(160, 170, 187, 0.25);
}

.agent-id {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 120px;
  flex-shrink: 0;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-dot.ok {
  background: var(--success);
  box-shadow: 0 0 5px var(--success);
}

.status-dot.warn {
  background: var(--warning);
}

.status-dot.err {
  background: var(--danger);
  animation: blink 1s infinite;
}

.agent-name {
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.score-bar-wrap {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 10px;
}

.score-bar {
  flex: 1;
  height: 5px;
  background: var(--bg-surface);
  border-radius: 3px;
  overflow: hidden;
}

.score-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 600ms ease;
}

.fill-success {
  background: var(--success);
}

.fill-warning {
  background: var(--warning);
}

.fill-danger {
  background: var(--danger);
}

.score-label {
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
  color: var(--text-muted);
  width: 26px;
  text-align: right;
  flex-shrink: 0;
}

.row-hint {
  font-size: 9px;
  color: var(--text-dim);
  opacity: 0;
  transition: opacity var(--transition);
  white-space: nowrap;
}

.agent-row:hover .row-hint {
  opacity: 1;
}

.no-agents {
  font-size: 12px;
  color: var(--text-dim);
  padding: 12px 0;
  text-align: center;
}

@keyframes blink {

  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.2;
  }
}
</style>