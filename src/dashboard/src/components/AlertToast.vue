<template>
  <Teleport to="body">
    <div class="toast-container">
      <TransitionGroup name="toast">
        <div v-for="toast in toasts" :key="toast.id" class="toast" :class="toastClass(toast)">
          <div class="toast-body" @click="goToAnomalies">
            <AlertTriangle :size="14" class="toast-icon" />
            <div class="toast-text">
              <span class="toast-host">{{ toast.hostName }}</span>
              <span class="toast-msg">{{ toastMessage(toast) }}</span>
            </div>
            <span class="toast-badge">{{ toastBadge(toast) }}</span>
          </div>
          <button class="toast-close" @click.stop="dismiss(toast.id)">
            <X :size="12" />
          </button>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { AlertTriangle, X } from 'lucide-vue-next'
import { useMonitoringStore } from '@/stores/monitoring'
import type { SchemaResolutionEvent } from '@/types'

const store = useMonitoringStore()
const router = useRouter()

interface Toast {
  id: string
  hostName: string
  type: 'schema' | 'anomaly'
  severity: 'critical' | 'high'
  raw?: SchemaResolutionEvent
}

const toasts = ref<Toast[]>([])

function toastClass(t: Toast) {
  return `toast-${t.severity}`
}

function toastMessage(t: Toast) {
  if (t.type === 'schema' && t.raw) {
    if (!t.raw.wasResolved)
      return `Unknown attribute: "${t.raw.rawAttribute}" — manual review required`
    return `"${t.raw.rawAttribute}" → "${t.raw.resolvedAttribute}" (${(t.raw.confidence * 100).toFixed(0)}% confidence)`
  }
  return 'System anomaly detected'
}

function toastBadge(t: Toast) {
  if (t.type === 'schema' && t.raw)
    return t.raw.wasResolved ? t.raw.resolutionTier : 'Unresolved'
  return t.severity === 'critical' ? 'Critical' : 'High'
}

watch(
  () => store.schemaEvents[0],
  (evt) => {
    if (!evt) return
    const severity = (!evt.wasResolved || evt.confidence < 0.60) ? 'critical'
      : evt.confidence < 0.75 ? 'high'
        : null
    if (!severity) return

    const toast: Toast = {
      id: evt.id + Date.now(),
      hostName: evt.hostName,
      type: 'schema',
      severity,
      raw: evt,
    }
    toasts.value.unshift(toast)
    if (toasts.value.length > 5) toasts.value.pop()
    setTimeout(() => dismiss(toast.id), 8000)
  }
)

function dismiss(id: string) {
  const i = toasts.value.findIndex(t => t.id === id)
  if (i !== -1) toasts.value.splice(i, 1)
}

function goToAnomalies() {
  router.push('/anomalies')
}
</script>

<style scoped>
.toast-container {
  position: fixed;
  bottom: 24px;
  right: 24px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  z-index: 9999;
  pointer-events: none;
}

.toast {
  display: flex;
  align-items: stretch;
  border-radius: var(--radius);
  border: 1px solid;
  background: var(--bg-panel);
  min-width: 320px;
  max-width: 440px;
  pointer-events: all;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
  overflow: hidden;
}

.toast-critical {
  border-color: color-mix(in srgb, var(--danger) 40%, transparent);
  background: color-mix(in srgb, var(--danger) 8%, var(--bg-panel));
}

.toast-high {
  border-color: color-mix(in srgb, var(--warning) 40%, transparent);
  background: color-mix(in srgb, var(--warning) 8%, var(--bg-panel));
}

.toast-body {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  flex: 1;
  cursor: pointer;
  min-width: 0;
}

.toast-body:hover {
  background: rgba(255, 255, 255, 0.03);
}

.toast-icon {
  flex-shrink: 0;
}

.toast-critical .toast-icon {
  color: var(--danger);
}

.toast-high .toast-icon {
  color: var(--warning);
}

.toast-text {
  flex: 1;
  min-width: 0;
}

.toast-host {
  display: block;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

.toast-msg {
  display: block;
  font-size: 11px;
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.toast-badge {
  font-family: var(--mono);
  font-size: 10px;
  font-weight: 700;
  flex-shrink: 0;
  padding: 2px 6px;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.05);
}

.toast-critical .toast-badge {
  color: var(--danger);
}

.toast-high .toast-badge {
  color: var(--warning);
}

.toast-close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  flex-shrink: 0;
  background: none;
  border: none;
  border-left: 1px solid rgba(255, 255, 255, 0.06);
  color: var(--text-dim);
  cursor: pointer;
  transition: all var(--transition);
}

.toast-close:hover {
  color: var(--text-muted);
  background: rgba(255, 255, 255, 0.04);
}

.toast-enter-active {
  transition: all 350ms cubic-bezier(0.16, 1, 0.3, 1);
}

.toast-leave-active {
  transition: all 200ms ease;
}

.toast-enter-from {
  opacity: 0;
  transform: translateX(40px) scale(0.95);
}

.toast-leave-to {
  opacity: 0;
  transform: translateX(40px);
}

.toast-move {
  transition: transform 300ms ease;
}
</style>