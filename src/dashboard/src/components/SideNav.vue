<template>
  <nav class="sidenav">
    <div class="brand">
      <span class="brand-name">System Monitor</span>
    </div>

    <ul class="nav-list">
      <li v-for="item in navItems" :key="item.path">
        <router-link :to="item.path" class="nav-item" active-class="active">
          <component :is="item.icon" :size="16" />
          <span>{{ item.label }}</span>
          <span v-if="item.badge" class="badge" :class="item.badgeClass">
            {{ item.badge }}
          </span>
        </router-link>
      </li>
    </ul>

    <div class="nav-footer">
      <div class="conn-status" :class="{ connected: store.isConnected }">
        <span class="dot" />
        <span>{{ store.isConnected ? 'Live' : 'Offline' }}</span>
      </div>
    </div>
  </nav>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import {
  LayoutDashboard, AlertTriangle, Activity,
  Server, Database, Settings, Cpu
} from 'lucide-vue-next'
import { useMonitoringStore } from '@/stores/monitoring'

const store = useMonitoringStore()

const navItems = computed(() => [
  { path: '/', label: 'Overview', icon: LayoutDashboard },
  {
    path: '/anomalies', label: 'Anomalies', icon: AlertTriangle,
    badge: store.criticalAnomalies.length || null,
    badgeClass: 'badge-danger'
  },
  { path: '/metrics', label: 'Metrics', icon: Activity },
  { path: '/agents', label: 'Agents', icon: Cpu },
  { path: '/logs', label: 'Logs', icon: Database },
  { path: '/infra', label: 'Infrastructure', icon: Server },
  {
    path: '/schema', label: 'Schema Registry', icon: Database,
    badge: null, badgeClass: 'badge-warn'
  },
  { path: '/training', label: 'Training Data', icon: Settings },
])
</script>

<style scoped>
.sidenav {
  width: 220px;
  flex-shrink: 0;
  background: var(--bg-panel);
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  padding: 0;
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px 20px 16px;
  border-bottom: 1px solid var(--border);
}

.brand-icon {
  font-size: 20px;
  color: var(--accent);
  line-height: 1;
}

.brand-name {
  font-family: var(--mono);
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: var(--text-primary);
}

.nav-list {
  list-style: none;
  padding: 12px 8px;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border-radius: var(--radius);
  color: var(--text-muted);
  text-decoration: none;
  font-size: 13px;
  font-weight: 500;
  transition: background var(--transition), color var(--transition);
}

.nav-item:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.nav-item.active {
  background: color-mix(in srgb, var(--accent) 12%, transparent);
  color: var(--accent);
}

.badge {
  margin-left: auto;
  font-family: var(--mono);
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 10px;
}

.badge-danger {
  background: color-mix(in srgb, var(--danger) 20%, transparent);
  color: var(--danger);
}

.nav-footer {
  padding: 16px 20px;
  border-top: 1px solid var(--border);
}

.conn-status {
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-muted);
}

.dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--text-dim);
  flex-shrink: 0;
}

.connected .dot {
  background: var(--success);
  box-shadow: 0 0 6px var(--success);
  animation: pulse 2s infinite;
}

.connected {
  color: var(--success);
}

@keyframes pulse {

  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.5;
  }
}
</style>
