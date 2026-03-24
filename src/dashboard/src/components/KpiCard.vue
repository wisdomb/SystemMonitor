<template>
  <component :is="to ? 'RouterLink' : 'div'" :to="to" class="kpi-card"
    :class="[`kpi-${color}`, { 'kpi-clickable': !!to }]">
    <div class="kpi-header">
      <span class="kpi-label">{{ label }}</span>
      <span class="kpi-icon-wrap">
        <component :is="iconComponent" :size="14" />
      </span>
    </div>
    <div class="kpi-value">
      <span class="value-num" :class="{ pulse }">{{ formattedValue }}</span>
      <span v-if="unit" class="value-unit">{{ unit }}</span>
    </div>
    <div v-if="total" class="kpi-sub">
      of {{ total }} total
    </div>

    <div v-if="to" class="kpi-arrow">
      <ArrowRight :size="12" />
    </div>
  </component>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import {
  Cpu, AlertTriangle, Zap, Activity,
  Heart, XCircle, Server, Database, ArrowRight
} from 'lucide-vue-next'

const iconMap: Record<string, any> = {
  'cpu': Cpu, 'alert-triangle': AlertTriangle, 'zap': Zap,
  'activity': Activity, 'heart': Heart, 'x-circle': XCircle,
  'server': Server, 'database': Database
}

const props = withDefaults(defineProps<{
  label: string
  value: number
  unit?: string
  total?: number
  icon?: string
  color?: 'accent' | 'success' | 'warning' | 'danger' | 'muted'
  pulse?: boolean
  to?: string
}>(), {
  icon: 'activity',
  color: 'accent',
  pulse: false,
})

const iconComponent = computed(() => iconMap[props.icon] ?? Activity)

const formattedValue = computed(() =>
  props.value >= 10_000
    ? (props.value / 1000).toFixed(1) + 'k'
    : props.value.toLocaleString()
)
</script>

<style scoped>
.kpi-card {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 16px;
  position: relative;
  overflow: hidden;
  display: block;
  text-decoration: none;
  color: inherit;
}

.kpi-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 2px;
  transition: height var(--transition);
}

.kpi-accent::before {
  background: var(--accent);
}

.kpi-success::before {
  background: var(--success);
}

.kpi-warning::before {
  background: var(--warning);
}

.kpi-danger::before {
  background: var(--danger);
}

.kpi-clickable {
  cursor: pointer;
  transition: border-color var(--transition), background var(--transition),
    transform var(--transition), box-shadow var(--transition);
}

.kpi-clickable:hover {
  border-color: var(--text-dim);
  background: var(--bg-hover);
  transform: translateY(-1px);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.2);
}

.kpi-clickable:hover::before {
  height: 3px;
}

.kpi-clickable:active {
  transform: translateY(0);
}

.kpi-arrow {
  position: absolute;
  bottom: 12px;
  right: 12px;
  color: var(--text-dim);
  opacity: 0;
  transition: opacity var(--transition), transform var(--transition);
}

.kpi-clickable:hover .kpi-arrow {
  opacity: 1;
  transform: translateX(2px);
}

.kpi-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.kpi-label {
  font-size: 11px;
  font-weight: 500;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.kpi-icon-wrap {
  color: var(--text-dim);
}

.kpi-value {
  display: flex;
  align-items: baseline;
  gap: 4px;
}

.value-num {
  font-family: var(--mono);
  font-size: 28px;
  font-weight: 700;
  line-height: 1;
}

.kpi-accent .value-num {
  color: var(--accent);
}

.kpi-success .value-num {
  color: var(--success);
}

.kpi-warning .value-num {
  color: var(--warning);
}

.kpi-danger .value-num {
  color: var(--danger);
}

.value-unit {
  font-family: var(--mono);
  font-size: 12px;
  color: var(--text-muted);
}

.kpi-sub {
  margin-top: 4px;
  font-size: 11px;
  color: var(--text-dim);
}

.pulse {
  animation: numPulse 1.2s ease-in-out;
}

@keyframes numPulse {

  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.5;
    transform: scale(1.05);
  }
}
</style>