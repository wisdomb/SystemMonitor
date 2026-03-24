<template>
  <div class="infra-view">
    <div class="infra-section">
      <h2 class="section-title">Service Bus Queues</h2>
      <div class="cards-row">
        <div class="infra-card">
          <div class="infra-card-icon" style="color:var(--accent)">
            <Layers :size="20" />
          </div>
          <div class="infra-card-body">
            <span class="infra-card-label">Metrics Queue Depth</span>
            <span class="infra-card-value" :class="queueColor(status?.metricQueueDepth ?? 0)">
              {{ (status?.metricQueueDepth ?? 0).toLocaleString() }}
            </span>
            <span class="infra-card-sub">messages pending</span>
          </div>
          <div class="infra-card-indicator" :class="queueColor(status?.metricQueueDepth ?? 0)" />
        </div>

        <div class="infra-card">
          <div class="infra-card-icon" style="color:var(--warning)">
            <FileText :size="20" />
          </div>
          <div class="infra-card-body">
            <span class="infra-card-label">Log Queue Depth</span>
            <span class="infra-card-value" :class="queueColor(status?.logQueueDepth ?? 0)">
              {{ (status?.logQueueDepth ?? 0).toLocaleString() }}
            </span>
            <span class="infra-card-sub">messages pending</span>
          </div>
          <div class="infra-card-indicator" :class="queueColor(status?.logQueueDepth ?? 0)" />
        </div>

        <div class="infra-card">
          <div class="infra-card-icon" style="color:var(--success)">
            <Cpu :size="20" />
          </div>
          <div class="infra-card-body">
            <span class="infra-card-label">Active Workers</span>
            <span class="infra-card-value ok">{{ status?.workerCount ?? 0 }}</span>
            <span class="infra-card-sub">processing pods</span>
          </div>
          <div class="infra-card-indicator ok" />
        </div>

        <div class="infra-card">
          <div class="infra-card-icon" style="color:var(--danger)">
            <Clock :size="20" />
          </div>
          <div class="infra-card-body">
            <span class="infra-card-label">Processing Lag</span>
            <span class="infra-card-value" :class="lagColor(status?.processingDelayMs ?? 0)">
              {{ status?.processingDelayMs ?? 0 }}ms
            </span>
            <span class="infra-card-sub">avg end-to-end</span>
          </div>
          <div class="infra-card-indicator" :class="lagColor(status?.processingDelayMs ?? 0)" />
        </div>
      </div>
    </div>

    <div class="infra-section">
      <h2 class="section-title">Azure Services</h2>
      <div class="services-table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Service</th>
              <th>Type</th>
              <th>Status</th>
              <th>Region</th>
              <th>SKU / Tier</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="svc in azureServices" :key="svc.name">
              <td style="font-weight:600; color:var(--text-primary)">{{ svc.name }}</td>
              <td style="font-family:var(--mono); font-size:11px">{{ svc.type }}</td>
              <td>
                <span class="status-pill" :class="svc.statusClass">{{ svc.status }}</span>
              </td>
              <td style="font-family:var(--mono); font-size:11px">{{ svc.region }}</td>
              <td style="color:var(--text-muted)">{{ svc.sku }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div class="infra-section">
      <h2 class="section-title">Kubernetes Workloads</h2>
      <div class="k8s-grid">
        <div v-for="w in k8sWorkloads" :key="w.name" class="k8s-card">
          <div class="k8s-name">{{ w.name }}</div>
          <div class="k8s-replicas">
            <span v-for="i in w.desired" :key="i" class="replica-dot" :class="i <= w.ready ? 'ready' : 'pending'" />
          </div>
          <div class="k8s-status">{{ w.ready }}/{{ w.desired }} ready</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { Layers, FileText, Cpu, Clock } from 'lucide-vue-next'
import { useMonitoringStore } from '@/stores/monitoring'
import { useIntervalFn } from '@vueuse/core'

const store = useMonitoringStore()
const status = computed(() => store.infraStatus)

onMounted(() => store.fetchInfrastructure())
useIntervalFn(() => store.fetchInfrastructure(), 10_000)

function queueColor(depth: number) {
  if (depth > 10_000) return 'err'
  if (depth > 1_000) return 'warn'
  return 'ok'
}

function lagColor(ms: number) {
  if (ms > 5000) return 'err'
  if (ms > 1000) return 'warn'
  return 'ok'
}

const azureServices = [
  { name: 'sysmon-servicebus', type: 'Microsoft.ServiceBus/namespaces', status: 'Running', statusClass: 'pill-ok', region: 'eastus', sku: 'Standard' },
  { name: 'sysmon-cosmos', type: 'Microsoft.DocumentDB/accounts', status: 'Running', statusClass: 'pill-ok', region: 'eastus', sku: 'Serverless' },
  { name: 'sysmon-signalr', type: 'Microsoft.SignalRService', status: 'Running', statusClass: 'pill-ok', region: 'eastus', sku: 'Standard S1' },
  { name: 'sysmon-storage', type: 'Microsoft.Storage/accounts', status: 'Running', statusClass: 'pill-ok', region: 'eastus', sku: 'LRS' },
  { name: 'sysmon-aks', type: 'Microsoft.ContainerService/managedClusters', status: 'Running', statusClass: 'pill-ok', region: 'eastus', sku: 'Standard B2s' },
  { name: 'sysmon-appinsights', type: 'Microsoft.Insights/components', status: 'Running', statusClass: 'pill-ok', region: 'eastus', sku: 'Pay-As-You-Go' },
]

const k8sWorkloads = [
  { name: 'api-service', desired: 3, ready: 3 },
  { name: 'worker', desired: 3, ready: 3 },
  { name: 'ai-service', desired: 2, ready: 2 },
  { name: 'agent', desired: 5, ready: 5 },
]
</script>

<style scoped>
.infra-view {
  display: flex;
  flex-direction: column;
  gap: 28px;
}

.section-title {
  font-family: var(--mono);
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-dim);
  margin-bottom: 12px;
  font-weight: 700;
}

.cards-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
}

.infra-card {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 18px;
  display: flex;
  align-items: center;
  gap: 14px;
  position: relative;
  overflow: hidden;
}

.infra-card-icon {
  flex-shrink: 0;
}

.infra-card-body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  flex: 1;
}

.infra-card-label {
  font-size: 11px;
  color: var(--text-muted);
}

.infra-card-value {
  font-family: var(--mono);
  font-size: 22px;
  font-weight: 700;
}

.infra-card-sub {
  font-size: 10px;
  color: var(--text-dim);
}

.infra-card-indicator {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 2px;
}

.ok {
  color: var(--success);
  background: var(--success);
}

.warn {
  color: var(--warning);
  background: var(--warning);
}

.err {
  color: var(--danger);
  background: var(--danger);
}

.services-table-wrap {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}

.status-pill {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 10px;
  font-weight: 700;
  font-family: var(--mono);
}

.pill-ok {
  background: color-mix(in srgb, var(--success) 15%, transparent);
  color: var(--success);
}

.pill-warn {
  background: color-mix(in srgb, var(--warning) 15%, transparent);
  color: var(--warning);
}

.pill-err {
  background: color-mix(in srgb, var(--danger) 15%, transparent);
  color: var(--danger);
}

.k8s-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 12px;
}

.k8s-card {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.k8s-name {
  font-family: var(--mono);
  font-size: 12px;
  color: var(--text-primary);
}

.k8s-replicas {
  display: flex;
  gap: 5px;
  flex-wrap: wrap;
}

.replica-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.replica-dot.ready {
  background: var(--success);
  box-shadow: 0 0 5px var(--success);
}

.replica-dot.pending {
  background: var(--warning);
}

.k8s-status {
  font-size: 11px;
  color: var(--text-muted);
}
</style>