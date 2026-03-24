<template>
  <div class="overview">

    <!-- Agent selector bar -->
    <div class="agent-bar">
      <div class="agent-bar-left">
        <span class="agent-bar-label">Viewing</span>

        <!-- "All Agents" pill -->
        <button class="agent-pill" :class="{ active: !selectedAgent }" @click="selectedAgent = ''">
          All Agents
          <span class="pill-count">{{ store.agentList.length }}</span>
        </button>

        <!-- One pill per active agent -->
        <button v-for="a in store.agentList" :key="a.agentId" class="agent-pill" :class="{
          active: selectedAgent === a.agentId,
          warn: a.score < 80 && a.score >= 50,
          crit: a.score < 50,
        }" @click="selectedAgent = a.agentId">
          <span class="pill-dot" :class="a.score >= 80 ? 'ok' : a.score >= 50 ? 'warn' : 'err'" />
          {{ a.agentId }}
          <span class="pill-score">{{ Math.round(a.score) }}</span>
        </button>
      </div>

      <!-- Context label -->
      <div class="agent-bar-right">
        <span v-if="selectedAgent" class="context-label">
          Showing data for <strong>{{ selectedAgent }}</strong>
        </span>
        <span v-else class="context-label">
          Showing aggregate across <strong>{{ store.agentList.length }}</strong>
          agent{{ store.agentList.length !== 1 ? 's' : '' }}
        </span>
      </div>
    </div>

    <!-- KPI Row -->
    <div class="kpi-row">
      <KpiCard label="Active Agents" :value="kpis.activeAgents" :total="store.summary?.totalAgents ?? 0" icon="cpu"
        color="accent" to="/agents" />
      <KpiCard label="Anomalies (1h)" :value="kpis.anomaliesLast1h" icon="alert-triangle" color="warning"
        to="/anomalies" :pulse="kpis.anomaliesLast1h > 0" />
      <KpiCard label="Critical Alerts" :value="kpis.criticalAlerts" icon="zap" color="danger" to="/anomalies"
        :pulse="kpis.criticalAlerts > 0" />
      <KpiCard label="Ingestion Rate" :value="store.summary?.ingestionRatePerMin ?? 0" unit="ev/min" icon="activity"
        color="success" to="/metrics" />
      <KpiCard label="Health Score" :value="kpis.healthScore" unit="%" icon="heart" to="/agents"
        :color="kpis.healthScore >= 80 ? 'success' : kpis.healthScore >= 50 ? 'warning' : 'danger'" />
      <KpiCard label="Error Rate" :value="+(store.summary?.errorRatePercent ?? 0).toFixed(2)" unit="%" icon="x-circle"
        color="danger" to="/logs" />
    </div>

    <!-- Main Grid -->
    <div class="grid-main">
      <!-- Left: Agent health + anomaly feed -->
      <div class="col-left">
        <PanelCard title="Agent Health">
          <AgentHealthList :highlight="selectedAgent" @select="selectedAgent = $event" />
        </PanelCard>

        <PanelCard title="Live Anomaly Feed">
          <AnomalyFeed :anomalies="filteredLiveFeed" :max="5" />
        </PanelCard>
      </div>

      <!-- Right: Charts + infra -->
      <div class="col-right">
        <PanelCard :title="chartTitle" class="chart-panel">
          <SystemOverviewChart :agent-id="selectedAgent || null" />
        </PanelCard>

        <PanelCard title="Infrastructure Status">
          <InfraStatus />
        </PanelCard>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useMonitoringStore } from '@/stores/monitoring'
import KpiCard from '@/components/KpiCard.vue'
import PanelCard from '@/components/PanelCard.vue'
import AgentHealthList from '@/components/AgentHealthList.vue'
import AnomalyFeed from '@/components/AnomalyFeed.vue'
import SystemOverviewChart from '@/components/SystemOverviewChart.vue'
import InfraStatus from '@/components/InfraStatus.vue'

const store = useMonitoringStore()
const selectedAgent = ref('')

// ── Filtered anomalies ────────────────────────────────────────────────────────
// Only live anomalies (SignalR pushed) for the overview feed — doesn't grow with polls
const filteredLiveFeed = computed(() => {
  const live = store.liveAnomalies
  if (!selectedAgent.value) return live
  return live.filter(a => a.agentId === selectedAgent.value)
})

const filteredAnomalies = computed(() => {
  if (!selectedAgent.value) return store.criticalAnomalies
  return store.criticalAnomalies.filter(a => a.agentId === selectedAgent.value)
})

// ── KPIs — agent-scoped or aggregate ─────────────────────────────────────────
const kpis = computed(() => {
  if (!selectedAgent.value) {
    // Aggregate — use store summary as-is
    return {
      activeAgents: store.summary?.activeAgents ?? 0,
      anomaliesLast1h: store.summary?.anomaliesLast1h ?? 0,
      criticalAlerts: store.summary?.criticalAlerts ?? 0,
      healthScore: store.overallHealth,
    }
  }

  // Single agent — filter down
  const agent = store.agentList.find(a => a.agentId === selectedAgent.value)
  const anomalies = store.criticalAnomalies.filter(a => a.agentId === selectedAgent.value)
  const critical = anomalies.filter(a => a.severity === 'Critical')

  return {
    activeAgents: 1,
    anomaliesLast1h: anomalies.length,
    criticalAlerts: critical.length,
    healthScore: Math.round(agent?.score ?? 100),
  }
})

const chartTitle = computed(() =>
  selectedAgent.value
    ? `${selectedAgent.value} — CPU & Memory`
    : 'System Overview — CPU & Memory'
)
</script>

<style scoped>
.overview {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* ── Agent selector bar ────────────────────────────────────────────────────── */
.agent-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 14px;
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  flex-wrap: wrap;
}

.agent-bar-left {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.agent-bar-label {
  font-family: var(--mono);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-dim);
  margin-right: 4px;
}

.agent-pill {
  display: flex;
  align-items: center;
  gap: 6px;
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
  background: rgba(160, 170, 187, 0.08);
  color: var(--accent);
}

.agent-pill.warn.active {
  border-color: var(--warning);
  background: rgba(255, 184, 48, 0.08);
  color: var(--warning);
}

.agent-pill.crit.active {
  border-color: var(--danger);
  background: rgba(255, 59, 107, 0.08);
  color: var(--danger);
}

.pill-count {
  padding: 0 5px;
  border-radius: 8px;
  font-size: 10px;
  background: var(--bg-hover);
  color: var(--text-dim);
}

.pill-score {
  padding: 0 5px;
  border-radius: 8px;
  font-size: 10px;
  background: var(--bg-hover);
}

.pill-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.pill-dot.ok {
  background: var(--success);
  box-shadow: 0 0 4px var(--success);
}

.pill-dot.warn {
  background: var(--warning);
}

.pill-dot.err {
  background: var(--danger);
  animation: blink 1s infinite;
}

.agent-bar-right {
  flex-shrink: 0;
}

.context-label {
  font-size: 12px;
  color: var(--text-dim);
}

.context-label strong {
  color: var(--text-muted);
}

/* ── KPI row ──────────────────────────────────────────────────────────────── */
.kpi-row {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 12px;
}

/* ── Main grid ────────────────────────────────────────────────────────────── */
.grid-main {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: 16px;
  align-items: start;
}

.col-left,
.col-right {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.chart-panel {
  min-height: 300px;
}

@media (max-width: 1200px) {
  .kpi-row {
    grid-template-columns: repeat(3, 1fr);
  }

  .grid-main {
    grid-template-columns: 1fr;
  }
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