<template>
  <div class="schema-view">

    <div class="stats-row">
      <div class="stat-chip" :class="stats.pending > 0 ? 'warn' : 'ok'">
        <AlertTriangle :size="13" />
        <span>{{ stats.pending }} pending review</span>
      </div>
      <div class="stat-chip ok">
        <CheckCircle :size="13" />
        <span>{{ canonicals.length }} canonical attributes</span>
      </div>
      <div class="stat-chip" :class="stats.pending > 0 ? 'warn' : 'ok'">
        <Activity :size="13" />
        <span>{{ (stats.resolutionRate * 100).toFixed(1) }}% resolution rate</span>
      </div>
      <div class="stat-chip accent">
        <Zap :size="13" />
        <span>{{ stats.highConfidence }} auto-resolvable</span>
      </div>
    </div>

    <div class="tab-bar">
      <button class="tab-btn" :class="{ active: tab === 'unknown' }" @click="tab = 'unknown'">
        Unknown Attributes
        <span v-if="unknowns.length" class="tab-badge">{{ unknowns.length }}</span>
      </button>
      <button class="tab-btn" :class="{ active: tab === 'canonical' }" @click="tab = 'canonical'">
        Canonical Registry
      </button>
      <button class="tab-btn" :class="{ active: tab === 'test' }" @click="tab = 'test'">
        Resolution Tester
      </button>
    </div>

    <div v-if="tab === 'unknown'" class="tab-content">
      <div v-if="unknowns.length === 0" class="empty-state">
        <CheckCircle :size="32" style="color:var(--success); margin-bottom:12px" />
        <p>All attributes resolved — no pending review items</p>
      </div>

      <div v-else class="unknown-list">
        <div v-for="u in unknowns" :key="u.id" class="unknown-card">
          <div class="unknown-header">
            <div class="unknown-name-wrap">
              <span class="raw-name">{{ u.rawName }}</span>
              <span class="occurrence-badge">{{ u.occurrenceCount }}× seen</span>
            </div>
            <div class="unknown-meta">
              <span class="meta-item">{{ u.sourceAgentId }}</span>
              <span class="meta-item">v{{ u.sourceVersion }}</span>
              <span class="meta-item">{{ formatTime(u.seenAt) }}</span>
            </div>
          </div>

          <div v-if="u.suggestedCanonicalId" class="suggestion-row">
            <span class="suggestion-label">Best match:</span>
            <span class="suggestion-value">{{ u.suggestedCanonicalId }}</span>
            <div class="conf-bar-wrap">
              <div class="conf-bar">
                <div class="conf-fill" :class="confColor(u.suggestionConfidence)"
                  :style="{ width: (u.suggestionConfidence * 100) + '%' }" />
              </div>
              <span class="conf-pct">{{ (u.suggestionConfidence * 100).toFixed(0) }}%</span>
            </div>
            <span class="method-badge">{{ u.suggestionMethod }}</span>
          </div>

          <div v-if="u.candidates?.length > 1" class="candidates">
            <span class="candidates-label">Other candidates:</span>
            <span v-for="c in u.candidates.slice(1, 4)" :key="c.canonicalId" class="candidate-chip">
              {{ c.canonicalName }} ({{ (c.score * 100).toFixed(0) }}%)
            </span>
          </div>

          <div class="review-actions">
            <select v-model="reviewSelections[u.id]" class="filter-select">
              <option value="">— Select canonical attribute —</option>
              <option v-for="c in canonicals" :key="c.id" :value="c.id">
                {{ c.name }} — {{ c.displayName }}
              </option>
            </select>
            <input v-model="reviewNotes[u.id]" class="note-input" placeholder="Note (optional)" />
            <button class="btn btn-primary" :disabled="!reviewSelections[u.id]" @click="confirm(u.id)">
              <CheckCircle :size="13" /> Confirm
            </button>
            <button class="btn btn-ghost" @click="reject(u.id)">
              <X :size="13" /> Reject
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="tab === 'canonical'" class="tab-content">
      <div class="toolbar">
        <input v-model="canonicalSearch" class="log-search" placeholder="Search attributes…" />
        <select v-model="categoryFilter" class="filter-select">
          <option value="">All Categories</option>
          <option v-for="cat in categories" :key="cat" :value="cat">{{ cat }}</option>
        </select>
        <button class="btn btn-primary" @click="showAddModal = true">
          <Plus :size="13" /> Add Attribute
        </button>
      </div>

      <div class="canonical-table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Display Name</th>
              <th>Category</th>
              <th>Unit</th>
              <th>Known Aliases</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="attr in filteredCanonicals" :key="attr.id">
              <td class="mono-cell">{{ attr.name }}</td>
              <td>{{ attr.displayName }}</td>
              <td>
                <span class="cat-badge">{{ attr.category }}</span>
              </td>
              <td class="mono-cell">{{ attr.unit || '—' }}</td>
              <td>
                <div class="alias-chips">
                  <span v-for="a in (attr.knownAliases ?? []).slice(0, 3)" :key="a" class="alias-chip">{{ a }}</span>
                  <span v-if="(attr.knownAliases ?? []).length > 3" class="alias-chip more">+{{ (attr.knownAliases ??
                    []).length - 3 }} more</span>
                </div>
              </td>
              <td>
                <button class="icon-btn" @click="editAttr = attr">
                  <Pencil :size="12" />
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div v-if="tab === 'test'" class="tab-content">
      <div class="tester-panel">
        <p class="tester-desc">
          Test how raw attribute names from a new customer config will resolve
          before they hit production. Paste a list (one per line) or test individually.
        </p>

        <div class="tester-inputs">
          <div class="tester-single">
            <label class="input-label">Single name</label>
            <div class="input-row">
              <input v-model="testSingle" class="log-search" placeholder="e.g. CPU Usage %" />
              <button class="btn btn-primary" @click="runSingleTest">
                <Play :size="13" /> Test
              </button>
            </div>
            <div v-if="singleResult" class="single-result" :class="singleResult.isResolved ? 'resolved' : 'unresolved'">
              <span class="result-icon">{{ singleResult.isResolved ? '✓' : '✗' }}</span>
              <span class="result-text">
                <template v-if="singleResult.isResolved">
                  Resolved to <strong>{{ singleResult.resolved?.name }}</strong>
                  via {{ singleResult.method }} ({{ (singleResult.confidence * 100).toFixed(0) }}%)
                </template>
                <template v-else>
                  Could not resolve — will be queued for review
                </template>
              </span>
            </div>
          </div>

          <div class="tester-bulk">
            <label class="input-label">Bulk test (one name per line)</label>
            <textarea v-model="bulkInput" class="bulk-textarea" rows="8"
              placeholder="processorUsage&#10;mem usage&#10;disk_write_mbps&#10;Full Name&#10;BytesReceived" />
            <button class="btn btn-primary" @click="runBulkTest" :disabled="!bulkInput.trim()">
              <Play :size="13" /> Run Bulk Test
            </button>
          </div>
        </div>

        <div v-if="bulkResults" class="bulk-results">
          <div class="bulk-summary">
            <span class="bulk-stat ok">{{ bulkResults.resolved }} resolved</span>
            <span class="bulk-stat warn">{{ bulkResults.unresolved }} unresolved</span>
            <span class="bulk-stat accent">{{ (bulkResults.rate * 100).toFixed(1) }}% rate</span>
          </div>
          <table class="data-table">
            <thead>
              <tr>
                <th>Raw Name</th>
                <th>Resolved To</th>
                <th>Method</th>
                <th>Confidence</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="r in bulkResults.results" :key="r.rawName" :class="r.isResolved ? '' : 'row-warn'">
                <td class="mono-cell">{{ r.rawName }}</td>
                <td class="mono-cell">{{ r.resolved?.name ?? '—' }}</td>
                <td><span class="method-badge">{{ r.method }}</span></td>
                <td>
                  <span v-if="r.isResolved" class="conf-num" :class="confColor(r.confidence)">
                    {{ (r.confidence * 100).toFixed(0) }}%
                  </span>
                  <span v-else style="color:var(--danger)">unresolved</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  AlertTriangle, CheckCircle, Activity, Zap, X, Plus,
  Pencil, Play
} from 'lucide-vue-next'
import { formatDistanceToNow } from 'date-fns'
import api from '@/utils/api'

const tab = ref<'unknown' | 'canonical' | 'test'>('unknown')
const unknowns = ref<any[]>([])
const canonicals = ref<any[]>([])
const stats = ref({
  pending: 0, withSuggestion: 0, highConfidence: 0,
  resolutionRate: 1, totalOccurrences: 0
})
const reviewSelections = ref<Record<string, string>>({})
const reviewNotes = ref<Record<string, string>>({})
const canonicalSearch = ref('')
const categoryFilter = ref('')
const showAddModal = ref(false)
const editAttr = ref<any>(null)
const testSingle = ref('')
const singleResult = ref<any>(null)
const bulkInput = ref('')
const bulkResults = ref<any>(null)

const categories = computed(() =>
  [...new Set(canonicals.value.map(c => c.category))].sort())

const filteredCanonicals = computed(() =>
  canonicals.value.filter(c => {
    const q = canonicalSearch.value.toLowerCase()
    const matchesSearch = !q ||
      c.name.toLowerCase().includes(q) ||
      c.displayName.toLowerCase().includes(q) ||
      (c.knownAliases ?? []).some((a: string) => a.toLowerCase().includes(q))
    const matchesCat = !categoryFilter.value || c.category === categoryFilter.value
    return matchesSearch && matchesCat
  })
)

async function load() {
  const [u, c, s] = await Promise.all([
    api.get('/api/v1/schema/unknown'),
    api.get('/api/v1/schema/canonical'),
    api.get('/api/v1/schema/unknown/stats')
  ])
  unknowns.value = u.data
  canonicals.value = c.data
  stats.value = { ...s.data, resolutionRate: s.data.pending === 0 ? 1 : 0.9 }
}

async function confirm(id: string) {
  const canonicalId = reviewSelections.value[id]
  if (!canonicalId) return
  await api.post(`/api/v1/schema/unknown/${id}/confirm`, {
    canonicalId,
    reviewedBy: 'analyst',
    note: reviewNotes.value[id] || null
  })
  await load()
}

async function reject(id: string) {
  await api.post(`/api/v1/schema/unknown/${id}/reject`, {
    reviewedBy: 'analyst', note: null
  })
  await load()
}

async function runSingleTest() {
  if (!testSingle.value.trim()) return
  const res = await api.post('/api/v1/schema/resolve/test', { rawName: testSingle.value })
  singleResult.value = res.data
}

async function runBulkTest() {
  const names = bulkInput.value.split('\n').map(s => s.trim()).filter(Boolean)
  const res = await api.post('/api/v1/schema/resolve/bulk-test', { rawNames: names })
  bulkResults.value = res.data
}

function confColor(c: number) {
  if (c >= 0.9) return 'fill-success'
  if (c >= 0.7) return 'fill-warning'
  return 'fill-danger'
}

function formatTime(ts: string) {
  return formatDistanceToNow(new Date(ts), { addSuffix: true })
}

onMounted(load)
</script>

<style scoped>
.schema-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.stats-row {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.stat-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 500;
  border: 1px solid;
}

.stat-chip.ok {
  background: color-mix(in srgb, var(--success) 10%, transparent);
  color: var(--success);
  border-color: color-mix(in srgb, var(--success) 25%, transparent);
}

.stat-chip.warn {
  background: color-mix(in srgb, var(--warning) 10%, transparent);
  color: var(--warning);
  border-color: color-mix(in srgb, var(--warning) 25%, transparent);
}

.stat-chip.accent {
  background: color-mix(in srgb, var(--accent) 10%, transparent);
  color: var(--accent);
  border-color: color-mix(in srgb, var(--accent) 25%, transparent);
}

.tab-bar {
  display: flex;
  gap: 0;
  border-bottom: 1px solid var(--border);
}

.tab-btn {
  padding: 10px 18px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-muted);
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  transition: all var(--transition);
}

.tab-btn:hover {
  color: var(--text-primary);
}

.tab-btn.active {
  color: var(--accent);
  border-bottom-color: var(--accent);
}

.tab-badge {
  background: color-mix(in srgb, var(--warning) 20%, transparent);
  color: var(--warning);
  font-size: 10px;
  font-family: var(--mono);
  font-weight: 700;
  padding: 1px 6px;
  border-radius: 10px;
}

.tab-content {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.unknown-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.unknown-card {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-left: 3px solid var(--warning);
  border-radius: var(--radius);
  padding: 16px 18px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.unknown-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 8px;
}

.unknown-name-wrap {
  display: flex;
  align-items: center;
  gap: 10px;
}

.raw-name {
  font-family: var(--mono);
  font-size: 15px;
  font-weight: 700;
  color: var(--text-primary);
}

.occurrence-badge {
  font-family: var(--mono);
  font-size: 10px;
  font-weight: 700;
  padding: 2px 7px;
  border-radius: 10px;
  background: color-mix(in srgb, var(--warning) 15%, transparent);
  color: var(--warning);
}

.unknown-meta {
  display: flex;
  gap: 10px;
}

.meta-item {
  font-size: 11px;
  color: var(--text-dim);
  font-family: var(--mono);
}

.suggestion-row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.suggestion-label {
  font-size: 11px;
  color: var(--text-dim);
  flex-shrink: 0;
}

.suggestion-value {
  font-family: var(--mono);
  font-size: 12px;
  font-weight: 700;
  color: var(--success);
}

.conf-bar-wrap {
  display: flex;
  align-items: center;
  gap: 6px;
}

.conf-bar {
  width: 80px;
  height: 4px;
  background: var(--bg-surface);
  border-radius: 2px;
  overflow: hidden;
}

.conf-fill {
  height: 100%;
  border-radius: 2px;
  transition: width 400ms ease;
}

.fill-success {
  background: var(--success);
}

.fill-warning {
  background: var(--warning);
}

.fill-danger {
  background: var(--danger);
}

.conf-pct {
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-muted);
}

.method-badge {
  font-family: var(--mono);
  font-size: 10px;
  font-weight: 700;
  padding: 2px 7px;
  border-radius: 4px;
  background: color-mix(in srgb, var(--accent) 12%, transparent);
  color: var(--accent);
  text-transform: uppercase;
}

.candidates {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.candidates-label {
  font-size: 11px;
  color: var(--text-dim);
}

.candidate-chip {
  font-family: var(--mono);
  font-size: 10px;
  padding: 2px 8px;
  border-radius: 4px;
  background: var(--bg-surface);
  color: var(--text-muted);
  border: 1px solid var(--border);
}

.review-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.filter-select {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-family: var(--sans);
  font-size: 12px;
  padding: 7px 10px;
  outline: none;
  flex: 1;
  min-width: 200px;
}

.note-input {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-family: var(--sans);
  font-size: 12px;
  padding: 7px 10px;
  outline: none;
  width: 160px;
}

.toolbar {
  display: flex;
  gap: 10px;
  align-items: center;
}

.log-search {
  flex: 1;
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-family: var(--sans);
  font-size: 12px;
  padding: 7px 10px;
  outline: none;
}

.canonical-table-wrap {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: auto;
}

.cat-badge {
  font-size: 10px;
  font-weight: 700;
  font-family: var(--mono);
  padding: 2px 8px;
  border-radius: 4px;
  background: color-mix(in srgb, var(--accent) 10%, transparent);
  color: var(--accent);
}

.alias-chips {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.alias-chip {
  font-family: var(--mono);
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 4px;
  background: var(--bg-surface);
  color: var(--text-muted);
  border: 1px solid var(--border);
}

.alias-chip.more {
  color: var(--accent);
  border-color: color-mix(in srgb, var(--accent) 25%, transparent);
}

.mono-cell {
  font-family: var(--mono);
  font-size: 11px;
}

.icon-btn {
  background: none;
  border: none;
  color: var(--text-dim);
  cursor: pointer;
  padding: 4px;
}

.icon-btn:hover {
  color: var(--accent);
}

.tester-panel {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.tester-desc {
  font-size: 13px;
  color: var(--text-muted);
}

.tester-inputs {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

.tester-single,
.tester-bulk {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.input-label {
  font-size: 11px;
  font-family: var(--mono);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--text-dim);
}

.input-row {
  display: flex;
  gap: 8px;
}

.bulk-textarea {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-family: var(--mono);
  font-size: 12px;
  padding: 10px;
  resize: vertical;
  outline: none;
  line-height: 1.6;
}

.single-result {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-radius: var(--radius);
  border: 1px solid;
  font-size: 12px;
}

.single-result.resolved {
  background: color-mix(in srgb, var(--success) 8%, transparent);
  border-color: color-mix(in srgb, var(--success) 25%, transparent);
  color: var(--success);
}

.single-result.unresolved {
  background: color-mix(in srgb, var(--danger) 8%, transparent);
  border-color: color-mix(in srgb, var(--danger) 25%, transparent);
  color: var(--danger);
}

.result-text strong {
  font-weight: 700;
}

.bulk-results {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.bulk-summary {
  display: flex;
  gap: 12px;
}

.bulk-stat {
  font-family: var(--mono);
  font-size: 12px;
  font-weight: 700;
  padding: 4px 10px;
  border-radius: 10px;
  border: 1px solid;
}

.bulk-stat.ok {
  color: var(--success);
  border-color: color-mix(in srgb, var(--success) 25%, transparent);
}

.bulk-stat.warn {
  color: var(--warning);
  border-color: color-mix(in srgb, var(--warning) 25%, transparent);
}

.bulk-stat.accent {
  color: var(--accent);
  border-color: color-mix(in srgb, var(--accent) 25%, transparent);
}

.row-warn td {
  background: color-mix(in srgb, var(--warning) 4%, transparent);
}

.conf-num.fill-success {
  color: var(--success);
}

.conf-num.fill-warning {
  color: var(--warning);
}

.conf-num.fill-danger {
  color: var(--danger);
}
</style>
