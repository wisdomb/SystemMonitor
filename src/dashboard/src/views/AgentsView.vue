<template>
  <div class="agents-view">
    <div class="agents-grid">
      <div v-for="agent in store.agentList" :key="agent.agentId" class="agent-card" :class="cardClass(agent.score)"
        @click="selectedAgent = selectedAgent === agent.agentId ? null : agent.agentId">
        <div class="agent-card-header">
          <div class="agent-id-row">
            <span class="status-glow" :class="statusClass(agent.score)" />
            <span class="agent-id-text">{{ agent.agentId }}</span>
          </div>
          <span class="score-chip" :class="statusClass(agent.score)">
            {{ Math.round(agent.score) }}
          </span>
        </div>

        <div class="score-ring-wrap">
          <svg viewBox="0 0 60 60" class="score-ring">
            <circle cx="30" cy="30" r="24" class="ring-track" />
            <circle cx="30" cy="30" r="24" class="ring-fill" :class="statusClass(agent.score)"
              :stroke-dasharray="`${(agent.score / 100) * 150.8} 150.8`" stroke-dashoffset="37.7" />
            <text x="30" y="35" class="ring-label">{{ Math.round(agent.score) }}</text>
          </svg>
        </div>

        <div class="agent-anomaly-count">
          {{ anomalyCount(agent.agentId) }} anomalies (1h)
        </div>
      </div>

      <div v-if="store.agentList.length === 0" class="no-agents-state">
        <Server :size="32" style="color:var(--text-dim); margin-bottom:12px" />
        <p>No agents reporting yet</p>
        <p style="font-size:11px; color:var(--text-dim); margin-top:4px">
          Deploy an agent to start collecting telemetry
        </p>
      </div>
    </div>

    <div v-if="selectedAgent" class="agent-detail">
      <div class="detail-header">
        <span class="detail-title">{{ selectedAgent }}</span>
        <button class="btn btn-ghost" @click="selectedAgent = null">
          <X :size="13" /> Close
        </button>
      </div>

      <div class="detail-body">
        <div class="detail-anomalies">
          <p class="detail-section-title">Recent Anomalies</p>
          <AnomalyFeed :anomalies="anomaliesForAgent(selectedAgent)" :max="10" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Server, X } from 'lucide-vue-next'
import { useMonitoringStore } from '@/stores/monitoring'
import AnomalyFeed from '@/components/AnomalyFeed.vue'

const store = useMonitoringStore()
onMounted(() => store.fetchHealthScores())
const selectedAgent = ref<string | null>(null)

function statusClass(score: number) {
  if (score >= 80) return 'ok'
  if (score >= 50) return 'warn'
  return 'err'
}

function cardClass(score: number) {
  if (score < 50) return 'card-critical'
  return ''
}

function anomalyCount(agentId: string) {
  return [...store.anomalies, ...store.liveAnomalies]
    .filter(a => a.agentId === agentId).length
}

function anomaliesForAgent(agentId: string) {
  return [...store.liveAnomalies, ...store.anomalies]
    .filter(a => a.agentId === agentId)
}
</script>

<style scoped>
.agents-view {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.agents-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 14px;
}

.agent-card {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 16px;
  cursor: pointer;
  transition: background var(--transition), border-color var(--transition), transform var(--transition);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.agent-card:hover {
  background: var(--bg-hover);
  transform: translateY(-1px);
}

.card-critical {
  border-color: color-mix(in srgb, var(--danger) 30%, transparent);
}

.agent-card-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.agent-id-row {
  display: flex;
  align-items: center;
  gap: 7px;
}

.status-glow {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-glow.ok {
  background: var(--success);
  box-shadow: 0 0 6px var(--success);
}

.status-glow.warn {
  background: var(--warning);
}

.status-glow.err {
  background: var(--danger);
  animation: blink 1s infinite;
}

.agent-id-text {
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 110px;
}

.score-chip {
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
  padding: 2px 7px;
  border-radius: 10px;
}

.score-chip.ok {
  background: color-mix(in srgb, var(--success) 15%, transparent);
  color: var(--success);
}

.score-chip.warn {
  background: color-mix(in srgb, var(--warning) 15%, transparent);
  color: var(--warning);
}

.score-chip.err {
  background: color-mix(in srgb, var(--danger) 15%, transparent);
  color: var(--danger);
}

.score-ring-wrap {
  width: 80px;
}

.score-ring {
  width: 100%;
}

.ring-track {
  fill: none;
  stroke: var(--bg-surface);
  stroke-width: 5;
}

.ring-fill {
  fill: none;
  stroke-width: 5;
  stroke-linecap: round;
  transition: stroke-dasharray 600ms ease;
}

.ring-fill.ok {
  stroke: var(--success);
}

.ring-fill.warn {
  stroke: var(--warning);
}

.ring-fill.err {
  stroke: var(--danger);
}

.ring-label {
  font-family: 'Space Mono', monospace;
  font-size: 13px;
  font-weight: 700;
  fill: var(--text-primary);
  text-anchor: middle;
}

.agent-anomaly-count {
  font-size: 11px;
  color: var(--text-dim);
}

.no-agents-state {
  grid-column: 1 / -1;
  padding: 60px;
  text-align: center;
  color: var(--text-muted);
  font-size: 13px;
}

.agent-detail {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 18px;
  border-bottom: 1px solid var(--border);
}

.detail-title {
  font-family: var(--mono);
  font-size: 13px;
  font-weight: 700;
  color: var(--text-primary);
}

.detail-body {
  padding: 16px 18px;
}

.detail-section-title {
  font-family: var(--mono);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-dim);
  margin-bottom: 12px;
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