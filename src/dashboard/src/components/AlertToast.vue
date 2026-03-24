<template>
  <Teleport to="body">
    <div class="toast-container">
      <TransitionGroup name="toast">
        <div v-for="toast in toasts" :key="toast.id" class="toast" :class="`toast-${toast.severity.toLowerCase()}`"
          @click="dismiss(toast.id)">
          <AlertTriangle :size="14" class="toast-icon" />
          <div class="toast-body">
            <span class="toast-host">{{ toast.hostName }}</span>
            <span class="toast-msg">{{ toast.description }}</span>
          </div>
          <span class="toast-conf">{{ (toast.confidence * 100).toFixed(0) }}%</span>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { AlertTriangle } from 'lucide-vue-next'
import { useMonitoringStore } from '@/stores/monitoring'
import { AnomalySeverity } from '@/types'
import type { AnomalyResult } from '@/types'

const store = useMonitoringStore()
interface Toast extends AnomalyResult { id: string }
const toasts = ref<Toast[]>([])

watch(
  () => store.liveAnomalies[0],
  (anomaly) => {
    if (!anomaly) return
    if (
      anomaly.severity !== AnomalySeverity.Critical &&
      anomaly.severity !== AnomalySeverity.High
    ) return

    const toast: Toast = { ...anomaly, id: anomaly.id + Date.now() }
    toasts.value.unshift(toast)
    if (toasts.value.length > 5) toasts.value.pop()

    setTimeout(() => dismiss(toast.id), 6000)
  }
)

function dismiss(id: string) {
  const i = toasts.value.findIndex(t => t.id === id)
  if (i !== -1) toasts.value.splice(i, 1)
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
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-radius: var(--radius);
  border: 1px solid;
  background: var(--bg-panel);
  min-width: 300px;
  max-width: 420px;
  pointer-events: all;
  cursor: pointer;
  backdrop-filter: blur(8px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
}

.toast-critical {
  border-color: color-mix(in srgb, var(--danger) 40%, transparent);
  background: color-mix(in srgb, var(--danger) 8%, var(--bg-panel));
}

.toast-high {
  border-color: color-mix(in srgb, var(--warning) 40%, transparent);
  background: color-mix(in srgb, var(--warning) 8%, var(--bg-panel));
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

.toast-body {
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

.toast-conf {
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
  flex-shrink: 0;
}

.toast-critical .toast-conf {
  color: var(--danger);
}

.toast-high .toast-conf {
  color: var(--warning);
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
