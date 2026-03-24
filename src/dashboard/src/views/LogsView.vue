<template>
  <div class="logs-view">

    <div class="log-tabs">
      <button class="log-tab" :class="{ active: tab === 'agent' }" @click="tab = 'agent'">
        Agent Logs
        <span class="tab-badge">{{ logs.length }}</span>
      </button>
      <button class="log-tab" :class="{ active: tab === 'activity' }" @click="tab = 'activity'; loadActivity()">
        Platform Activity
        <span class="tab-badge activity" v-if="activityLogs.length">{{ activityLogs.length }}</span>
      </button>
    </div>

    <template v-if="tab === 'agent'">
      <div class="log-toolbar">
        <div class="level-filters">
          <button v-for="lvl in levels" :key="lvl.key" class="level-btn"
            :class="[`lvl-${lvl.key.toLowerCase()}`, { active: activeLevel === lvl.key }]"
            @click="activeLevel = activeLevel === lvl.key ? null : lvl.key">
            {{ lvl.label }}
          </button>
        </div>

        <input v-model="searchText" class="log-search" placeholder="Search messages..." />

        <select v-model="selectedService" class="filter-select">
          <option value="">All Services</option>
          <option v-for="s in services" :key="s" :value="s">{{ s }}</option>
        </select>

        <label class="auto-scroll-toggle">
          <input type="checkbox" v-model="autoScroll" />
          Auto-scroll
        </label>

        <button class="btn btn-ghost" @click="clearLogs">
          <Trash2 :size="13" /> Clear
        </button>

        <button class="btn btn-ghost" @click="loadLogs">
          <RefreshCw :size="13" /> Refresh
        </button>
      </div>

      <div ref="terminalRef" class="log-terminal scroll-y" @scroll="onScroll">
        <div v-for="(log, idx) in filteredLogs" :key="log.id ?? idx" class="log-line"
          :class="`log-line-${log.level?.toLowerCase()}`">
          <span class="log-ts">{{ formatTs(log.timestamp) }}</span>
          <span class="log-host">{{ log.hostName }}</span>
          <span class="log-svc">{{ log.serviceName }}</span>
          <span class="log-lvl" :class="`log-${log.level?.toLowerCase()}`">{{ log.level }}</span>
          <span class="log-msg">{{ log.message }}</span>
        </div>

        <div v-if="filteredLogs.length === 0" class="empty-state">
          <p v-if="logs.length === 0">No agent logs yet</p>
          <p v-else>No log entries present</p>
        </div>
      </div>
    </template>

    <template v-if="tab === 'activity'">
      <div class="log-toolbar">
        <span class="toolbar-label">System events — training uploads, ingestion, errors</span>
        <button class="btn btn-ghost" @click="loadActivity">
          <RefreshCw :size="13" /> Refresh
        </button>
      </div>

      <div class="log-terminal scroll-y">
        <div v-for="(entry, i) in activityLogs" :key="i" class="log-line" :class="`log-line-${entry.level}`">
          <span class="log-ts">{{ formatTs(entry.timestamp) }}</span>
          <span class="log-lvl" :class="`log-${entry.level}`">{{ entry.level?.toUpperCase() }}</span>
          <span class="log-msg">{{ entry.message }}</span>
        </div>

        <div v-if="activityLogs.length === 0" class="empty-state">
          <p>No platform activity yet — upload training data or wait for the agent to connect</p>
        </div>
      </div>
    </template>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted } from 'vue'
import { Trash2, RefreshCw } from 'lucide-vue-next'
import { format } from 'date-fns'
import api from '@/utils/api'
import type { LogEvent } from '@/types'

const tab = ref<'agent' | 'activity'>('agent')
const terminalRef = ref<HTMLElement | null>(null)
const autoScroll = ref(true)
const activeLevel = ref<string | null>(null)
const searchText = ref('')
const selectedService = ref('')
const logs = ref<LogEvent[]>([])
const activityLogs = ref<{ timestamp: string; message: string; level: string }[]>([])

const levels = [
  { key: 'Critical', label: 'CRIT' },
  { key: 'Error', label: 'ERR' },
  { key: 'Warning', label: 'WARN' },
  { key: 'Information', label: 'INFO' },
  { key: 'Debug', label: 'DEBUG' },
]

const services = computed(() =>
  [...new Set(logs.value.map(l => l.serviceName))].filter(Boolean).sort()
)

const filteredLogs = computed(() =>
  logs.value.filter(l => {
    if (activeLevel.value && l.level !== activeLevel.value) return false
    if (selectedService.value && l.serviceName !== selectedService.value) return false
    if (searchText.value &&
      !l.message?.toLowerCase().includes(searchText.value.toLowerCase())) return false
    return true
  })
)

function formatTs(ts: string) {
  try { return format(new Date(ts), 'HH:mm:ss.SSS') }
  catch { return ts }
}

function clearLogs() { logs.value = [] }

function onScroll() {
  if (!terminalRef.value) return
  const el = terminalRef.value
  const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40
  autoScroll.value = atBottom
}

function scrollToBottom() {
  nextTick(() => {
    if (terminalRef.value)
      terminalRef.value.scrollTop = terminalRef.value.scrollHeight
  })
}

watch(filteredLogs, () => {
  if (autoScroll.value) scrollToBottom()
})

async function loadLogs() {
  try {
    const res = await api.get('/api/v1/analytics/logs/recent', { params: { limit: 200 } })
    logs.value = res.data ?? []
  } catch { }
}

async function loadActivity() {
  try {
    const res = await api.get('/api/v1/analytics/activity', { params: { limit: 100 } })
    activityLogs.value = res.data ?? []
  } catch { }
}

onMounted(() => {
  loadLogs()
  loadActivity()
})
</script>

<style scoped>
.logs-view {
  display: flex;
  flex-direction: column;
  gap: 12px;
  height: 100%;
}

.log-tabs {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--border);
  padding-bottom: 0;
}

.log-tab {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--text-dim);
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  cursor: pointer;
  margin-bottom: -1px;
  transition: all var(--transition);
}

.log-tab:hover {
  color: var(--text-muted);
}

.log-tab.active {
  color: var(--accent);
  border-bottom-color: var(--accent);
}

.tab-badge {
  padding: 1px 6px;
  border-radius: 10px;
  font-size: 10px;
  background: var(--bg-surface);
  color: var(--text-dim);
}

.tab-badge.activity {
  background: color-mix(in srgb, var(--accent) 15%, transparent);
  color: var(--accent);
}

.log-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.toolbar-label {
  font-size: 12px;
  color: var(--text-dim);
  flex: 1;
}

.level-filters {
  display: flex;
  gap: 4px;
}

.level-btn {
  font-family: var(--mono);
  font-size: 10px;
  font-weight: 700;
  padding: 4px 9px;
  border-radius: 4px;
  border: 1px solid var(--border);
  background: var(--bg-surface);
  color: var(--text-muted);
  cursor: pointer;
  transition: all var(--transition);
}

.level-btn.active,
.level-btn:hover {
  color: var(--text-primary);
  background: var(--bg-hover);
}

.lvl-critical.active {
  border-color: var(--danger);
  color: var(--danger);
  background: color-mix(in srgb, var(--danger) 12%, transparent);
}

.lvl-error.active {
  border-color: var(--danger);
  color: var(--danger);
  background: color-mix(in srgb, var(--danger) 10%, transparent);
}

.lvl-warning.active {
  border-color: var(--warning);
  color: var(--warning);
  background: color-mix(in srgb, var(--warning) 10%, transparent);
}

.lvl-information.active {
  border-color: var(--accent);
  color: var(--accent);
  background: color-mix(in srgb, var(--accent) 10%, transparent);
}

.lvl-debug.active {
  border-color: var(--text-dim);
  color: var(--text-muted);
}

.log-search {
  flex: 1;
  max-width: 280px;
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-size: 12px;
  padding: 6px 10px;
  outline: none;
}

.log-search:focus {
  border-color: var(--accent);
}

.filter-select {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-size: 12px;
  padding: 6px 10px;
  outline: none;
}

.auto-scroll-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
  cursor: pointer;
}

.log-terminal {
  flex: 1;
  min-height: 400px;
  max-height: 600px;
  background: #070c14;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 8px 0;
  overflow-y: auto;
}

.log-line {
  display: flex;
  gap: 10px;
  padding: 2px 14px;
  font-family: var(--mono);
  font-size: 12px;
  line-height: 1.7;
  border-left: 2px solid transparent;
}

.log-line-critical,
.log-line-error {
  border-left-color: var(--danger);
  background: color-mix(in srgb, var(--danger) 3%, transparent);
}

.log-line-warning {
  border-left-color: var(--warning);
}

.log-line-success {
  border-left-color: var(--success);
}

.log-ts {
  color: var(--text-dim);
  flex-shrink: 0;
  min-width: 90px;
}

.log-host {
  color: #4a6fa5;
  flex-shrink: 0;
  min-width: 100px;
}

.log-svc {
  color: #5a7a6a;
  flex-shrink: 0;
  min-width: 120px;
}

.log-lvl {
  flex-shrink: 0;
  min-width: 60px;
  font-weight: 700;
  font-size: 10px;
}

.log-msg {
  color: var(--text-muted);
}

.log-critical {
  color: var(--danger);
}

.log-error {
  color: var(--danger);
}

.log-warning {
  color: var(--warning);
}

.log-information {
  color: var(--accent);
}

.log-debug {
  color: var(--text-dim);
}

.log-info {
  color: var(--accent);
}

.log-success {
  color: var(--success);
}

.log-warn {
  color: var(--warning);
}
</style>