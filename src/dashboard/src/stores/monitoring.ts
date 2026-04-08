import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  AnomalyResult, HealthScore, DashboardSummary,
  InfrastructureStatus, MetricEvent, TimeSeriesPoint,
  SchemaResolutionEvent
} from '@/types'
import { AnomalySeverity } from '@/types'
import api from '@/utils/api'

export const useMonitoringStore = defineStore('monitoring', () => {
  const summary = ref<DashboardSummary | null>(null)
  const anomalies = ref<AnomalyResult[]>([])
  const healthScores = ref<Map<string, number>>(new Map())
  const infraStatus = ref<InfrastructureStatus | null>(null)
  const recentMetrics = ref<MetricEvent[]>([])
  const liveAnomalies = ref<AnomalyResult[]>([])
  const schemaEvents = ref<SchemaResolutionEvent[]>([])
  const acknowledged = ref<Set<string>>(new Set())
  const ingestionRate = ref(0)
  const isConnected = ref(false)
  const isLoading = ref(false)
  const lastError = ref<string | null>(null)

  const tenants = ref<any[]>([])
  const selectedTenant = ref<string | null>(null)

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

  const criticalSchemaEvents = computed(() =>
    schemaEvents.value.filter(e => !e.wasResolved || e.confidence < 0.60)
  )

  async function fetchSummary() {
    try {
      const res = await api.get<DashboardSummary>('/api/v1/analytics/summary')
      summary.value = res.data
    } catch (e) { console.error('fetchSummary failed', e) }
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

  async function fetchSchemaEvents() {
    try {
      const res = await api.get<SchemaResolutionEvent[]>('/api/v1/analytics/schema-events')
      const incoming = res.data ?? []
      incoming.forEach(evt => {
        if (!schemaEvents.value.find(e => e.id === evt.id))
          schemaEvents.value.unshift(evt)
      })
      if (schemaEvents.value.length > 200)
        schemaEvents.value = schemaEvents.value.slice(0, 200)
    } catch { }
  }

  async function fetchHealthScores() {
    try {
      const res = await api.get<Record<string, number>>('/api/v1/analytics/health')
      healthScores.value = new Map(Object.entries(res.data))
    } catch (e) { console.error('fetchHealthScores failed', e) }
  }

  async function fetchInfrastructure() {
    try {
      const res = await api.get<InfrastructureStatus>('/api/v1/analytics/infrastructure')
      infraStatus.value = res.data
    } catch (e) { console.error('fetchInfrastructure failed', e) }
  }

  async function fetchTimeSeries(agentId: string, metricKey: string, windowMinutes = 30) {
    const res = await api.get<TimeSeriesPoint[]>(
      `/api/v1/analytics/metrics/${agentId}/${metricKey}`,
      { params: { windowMinutes } }
    )
    return res.data
  }

  async function fetchTenants() {
    try {
      const res = await api.get('/api/v1/tenants')
      tenants.value = res.data ?? []
    } catch (e) { console.error('fetchTenants failed', e) }
  }

  async function fetchTenantOverview(tenantId: string) {
    try {
      const res = await api.get(`/api/v1/tenants/${tenantId}/overview`)
      if (res.data) {
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
        if (res.data.agentScores)
          healthScores.value = new Map(Object.entries(res.data.agentScores))
      }
    } catch (e) { console.error('fetchTenantOverview failed', e) }
  }

  async function selectTenant(tenantId: string | null) {
    selectedTenant.value = tenantId
    if (tenantId) await fetchTenantOverview(tenantId)
    else await initialize()
  }

  function onLiveAnomaly(anomaly: AnomalyResult) {
    liveAnomalies.value.unshift(anomaly)
    if (liveAnomalies.value.length > 50)
      liveAnomalies.value = liveAnomalies.value.slice(0, 50)
    if (summary.value) summary.value.anomaliesLast1h++
    const s = anomaly.severity as any
    const isCrit = s === AnomalySeverity.Critical || s === 3 || String(s).toLowerCase() === 'critical'
    if (isCrit && summary.value) summary.value.criticalAlerts++
  }

  function onSchemaEvent(evt: SchemaResolutionEvent) {
    if (!schemaEvents.value.find(e => e.id === evt.id)) {
      schemaEvents.value.unshift(evt)
      if (schemaEvents.value.length > 200)
        schemaEvents.value = schemaEvents.value.slice(0, 200)
    }
  }

  function acknowledgeItem(id: string) {
    acknowledged.value = new Set([...acknowledged.value, id])
  }

  function unacknowledgeItem(id: string) {
    const s = new Set(acknowledged.value)
    s.delete(id)
    acknowledged.value = s
  }

  function isAcknowledged(id: string) {
    return acknowledged.value.has(id)
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

  async function initialize() {
    await Promise.all([
      fetchSummary(),
      fetchAnomalies(),
      fetchHealthScores(),
      fetchInfrastructure(),
      fetchSchemaEvents(),
    ])
  }

  async function refresh() {
    await Promise.all([
      fetchSummary(),
      fetchHealthScores(),
      fetchAnomalies(),
      fetchSchemaEvents(),
    ])
  }

  return {
    summary, anomalies, healthScores, infraStatus, recentMetrics,
    liveAnomalies, schemaEvents, acknowledged, ingestionRate,
    isConnected, isLoading, lastError,
    criticalAnomalies, criticalSchemaEvents, overallHealth, agentList,
    tenants, selectedTenant,
    fetchSummary, fetchAnomalies, fetchHealthScores, fetchInfrastructure,
    fetchSchemaEvents, fetchTenants, fetchTenantOverview, selectTenant,
    fetchTimeSeries, refresh, initialize,
    onLiveAnomaly, onSchemaEvent, acknowledgeItem, unacknowledgeItem, isAcknowledged,
    onHealthScoreUpdate, onMetricBatchReceived, setConnected,
  }
})