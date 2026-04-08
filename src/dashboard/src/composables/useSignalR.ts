import { onMounted, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useMonitoringStore } from '@/stores/monitoring'
import type { AnomalyResult, SchemaResolutionEvent } from '@/types'

let connection: signalR.HubConnection | null = null

export function useSignalR() {
  const store = useMonitoringStore()

  async function connect() {
    if (connection) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/monitoring')
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
          if (retryContext.elapsedMilliseconds < 60_000)
            return Math.random() * 5_000 + 1_000
          return null
        }
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.onreconnecting(() => store.setConnected(false))
    connection.onreconnected(() => store.setConnected(true))
    connection.onclose(() => store.setConnected(false))

    connection.on('AnomalyDetected', (anomaly: AnomalyResult) => {
      store.onLiveAnomaly(anomaly)
    })

    connection.on('BroadcastAnomaly', (anomaly: AnomalyResult) => {
      store.onLiveAnomaly(anomaly)
    })

    connection.on('SchemaEventDetected', (evt: SchemaResolutionEvent) => {
      store.onSchemaEvent(evt)
    })

    connection.on('BroadcastHealthScore', (payload: { agentId: string; score: number }) => {
      store.onHealthScoreUpdate(payload.agentId, payload.score)
    })

    connection.on('MetricBatchReceived', (payload: { count: number }) => {
      store.onMetricBatchReceived(payload.count)
    })

    connection.on('HealthScoreUpdated', (agentId: string, score: number) => {
      store.onHealthScoreUpdate(agentId, score)
    })

    try {
      await connection.start()
      store.setConnected(true)
    } catch (err) {
      console.error('[SignalR] Connection failed', err)
      store.setConnected(false)
    }
  }

  async function disconnect() {
    if (connection) {
      await connection.stop()
      connection = null
      store.setConnected(false)
    }
  }

  async function subscribeToAgent(agentId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected)
      await connection.invoke('SubscribeToAgent', agentId)
  }

  onMounted(connect)
  onUnmounted(disconnect)

  return { connect, disconnect, subscribeToAgent }
}