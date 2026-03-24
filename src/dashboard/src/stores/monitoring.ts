import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  AnomalyResult, HealthScore, DashboardSummary,
  InfrastructureStatus, MetricEvent, TimeSeriesPoint
} from '@/types'
import { AnomalySeverity } from '@/types'
import api from '@/utils/api'

export const useMonitoringStore = defineStore('monitoring', () => {
  // ── State ──────────────────────────────────────────────────────────────────
  const summary = ref<DashboardSummary | null>(null)
  const anomalies = ref<AnomalyResult[]>([])
  const healthScores = ref<Map<string, number>>(new Map())
  const infraStatus = ref<InfrastructureStatus | null>(null)
  const recentMetrics = ref<MetricEvent[]>([])
  const liveAnomalies = ref<AnomalyResult[]>([])    // pushed via SignalR
  const ingestionRate = ref(0)    // events/min rolling counter
  const isConnected = ref(false)

  // ── Tenant state ────────────────────────────────────────────────────────────
  const tenants = ref<any[]>([])
  const selectedTenant = ref<string | null>(null)  // null = all tenants view

  // Loading / error states
  const isLoading = ref(false)
  const lastError = ref<string | null>(null)

  // ── Getters ────────────────────────────────────────────────────────────────
  const criticalAnomalies = computed(() =>
    [...anomalies.value, ...liveAnomalies.value]
      .filter(a => {
        const s = a.severity as any
        return s === AnomalySeverity.Critical
          || s === 3
          || String(s).toLowerCase() === 'critical'
      })
      .sort((a, b) => b.detectedAt.localeCompare(a.detectedAt))
      .slice(0, 20)
  )

  const overallHealth = computed(() => {
    const scores = [...healthScores.value.values()]
    if (scores.length === 0) return 0
    return Math.round(scores.reduce((a, b) => a + b, 0) / scores.length)
  })

  const agentList = computed(() =>
    [...healthScores.value.entries()].map(([agentId, score]) => ({ agentId, score }))
  )

  // ── Actions ────────────────────────────────────────────────────────────────
  async function fetchSummary() {
    try {
      const res = await api.get<DashboardSummary>('/api/v1/analytics/summary')
      summary.value = res.data
    } catch (e) {
      console.error('fetchSummary failed', e)
    }
  }

  async function fetchAnomalies(offsetMinutes = 60) {
    try {
      isLoading.value = true
      const res = await api.get<AnomalyResult[]>('/api/v1/analytics/anomalies', {
        params: { offsetMinutes, limit: 100 }
      })
      anomalies.value = res.data
    } catch (e) {
      lastError.value = 'Failed to load anomalies'
    } finally {
      isLoading.value = false
    }
  }

  async function fetchHealthScores() {
    try {
      const res = await api.get<Record<string, number>>('/api/v1/analytics/health')
      healthScores.value = new Map(Object.entries(res.data))
    } catch (e) {
      console.error('fetchHealthScores failed', e)
    }
  }

  async function fetchInfrastructure() {
    try {
      const res = await api.get<InfrastructureStatus>('/api/v1/analytics/infrastructure')
      infraStatus.value = res.data
    } catch (e) {
      console.error('fetchInfrastructure failed', e)
    }
  }

  async function fetchTimeSeries(agentId: string, metricKey: string, windowMinutes = 30) {
    const res = await api.get<TimeSeriesPoint[]>(
      `/api/v1/analytics/metrics/${agentId}/${metricKey}`,
      { params: { windowMinutes } }
    )
    return res.data
  }

  // ── Tenant actions ──────────────────────────────────────────────────────────

  async function fetchTenants() {
    try {
      const res = await api.get('/api/v1/tenants')
      tenants.value = res.data ?? []
    } catch (e) {
      console.error('fetchTenants failed', e)
    }
  }

  async function fetchTenantOverview(tenantId: string) {
    try {
      const res = await api.get(`/api/v1/tenants/${tenantId}/overview`)
      if (res.data) {
        // Merge tenant-scoped data into summary
        summary.value = {
          ...summary.value,
          activeAgents: res.data.activeAgents,
          anomaliesLast1h: res.data.anomaliesLast1h,
          criticalAlerts: res.data.criticalAlerts,
          avgHealthScore: res.data.avgHealthScore,
          ingestionRatePerMin: res.data.ingestionRate ?? 0,
          totalAgents: res.data.activeAgents,
          errorRatePercent: 0,
          avgLatencyMs: 0
        }
        // Update health scores for this tenant only
        if (res.data.agentScores) {
          healthScores.value = new Map(Object.entries(res.data.agentScores))
        }
      }
    } catch (e) {
      console.error('fetchTenantOverview failed', e)
    }
  }

  async function selectTenant(tenantId: string | null) {
    selectedTenant.value = tenantId
    if (tenantId) {
      await fetchTenantOverview(tenantId)
    } else {
      await initialize()
    }
  }

  // Called by SignalR composable to push live events
  function onLiveAnomaly(anomaly: AnomalyResult) {
    liveAnomalies.value.unshift(anomaly)
    // Cap at 50 — the feed component slices to its own max
    if (liveAnomalies.value.length > 50)
      liveAnomalies.value = liveAnomalies.value.slice(0, 50)

    if (summary.value) summary.value.anomaliesLast1h++
    if (anomaly.severity === AnomalySeverity.Critical && summary.value)
      summary.value.criticalAlerts++
  }

  function onHealthScoreUpdate(agentId: string, score: number) {
    healthScores.value.set(agentId, score)
  }

  function onMetricBatchReceived(count: number) {
    ingestionRate.value = count
  }

  function setConnected(connected: boolean) {
    isConnected.value = connected
  }

  // Initial load
  async function initialize() {
    await Promise.all([
      fetchSummary(),
      fetchAnomalies(),
      fetchHealthScores(),
      fetchInfrastructure()
    ])
  }

  // Called by App.vue on an interval to keep data fresh
  async function refresh() {
    await Promise.all([
      fetchSummary(),
      fetchHealthScores(),
      fetchAnomalies()
    ])
  }

  return {
    summary, anomalies, healthScores, infraStatus, recentMetrics,
    liveAnomalies, ingestionRate, isConnected, isLoading, lastError,
    criticalAnomalies, overallHealth, agentList,
    tenants, selectedTenant, fetchTenants, fetchTenantOverview, selectTenant,
    fetchSummary, fetchAnomalies, fetchHealthScores, fetchInfrastructure, refresh,
    fetchTimeSeries, onLiveAnomaly, onHealthScoreUpdate,
    onMetricBatchReceived, setConnected, initialize
  }
})