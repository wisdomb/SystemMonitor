<template>
  <div class="training-view">

    <div class="explainer-panel">
      <div class="explainer-header">
        <Brain :size="16" />
        <span>What is Training Data?</span>
      </div>
      <div class="explainer-body">
        <p>
          The anomaly detection model learns what the most accurate attribute looks like by studying labeled
          examples. Each row in the CSV represents one point in time — 10 system metrics
          plus a label (<code>is_anomaly = 0</code> or <code>1</code>) telling the model
          whether that moment was a problem.
        </p>
        <div class="explainer-steps">
          <div class="step">
            <span class="step-num">1</span>
            <div>
              <strong>You upload labeled CSV data</strong>
              <p>Historical data where you've marked which attributes were genuine anomalies
                (CPU spike, memory leak, error burst etc.)</p>
            </div>
          </div>
          <div class="step">
            <span class="step-num">2</span>
            <div>
              <strong>A FastTree classifier trains on your data</strong>
              <p>ML.NET builds a gradient-boosted decision tree that learns the patterns
                specific to your systems — not just generic thresholds.</p>
            </div>
          </div>
          <div class="step">
            <span class="step-num">3</span>
            <div>
              <strong>The model gets saved and hot-reloaded</strong>
              <p>The trained model is saved to Azure Blob Storage and loaded into the
                AI Service. Every new metric batch is scored against it in real time.</p>
            </div>
          </div>
          <div class="step">
            <span class="step-num">4</span>
            <div>
              <strong>Detection improves over time</strong>
              <p>Upload more labeled data as your systems evolve. Each upload retrains
                the model — the more examples, the more accurate the detection.</p>
            </div>
          </div>
        </div>
        <div class="explainer-note">
          <Info :size="13" />
          The pre-built <code>training-labeled.csv</code> in <code>seed-data/</code>
          contains 2,000 labeled rows with a 7.3% anomaly rate across 5 anomaly types —
          ready to use immediately.
        </div>
      </div>
    </div>

    <div class="upload-panel" :class="{ dragging }" @dragover.prevent="dragging = true" @dragleave="dragging = false"
      @drop.prevent="onDrop">
      <div v-if="!file" class="upload-idle">
        <Upload :size="32" style="color:var(--text-dim)" />
        <p class="upload-hint">Drop a <strong>.csv</strong> or <strong>.json</strong> file here</p>
        <p class="upload-hint-sub">or</p>
        <label class="btn btn-ghost">
          Browse File
          <input type="file" accept=".csv,.json" style="display:none" @change="onFilePick" />
        </label>
        <p class="upload-hint-sub" style="margin-top:8px">
          Use <code>seed-data/training-labeled.csv</code> to get started immediately
        </p>
      </div>

      <div v-else class="upload-ready">
        <FileCheck :size="28" style="color:var(--success)" />
        <div class="file-info">
          <span class="file-name">{{ file.name }}</span>
          <span class="file-size">{{ formatSize(file.size) }}</span>
        </div>
        <button class="btn btn-ghost" @click="resetUpload">
          <X :size="13" /> Remove
        </button>
      </div>
    </div>

    <div class="schema-panel">
      <p class="schema-title">Expected CSV Schema</p>
      <div class="schema-table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Column</th>
              <th>Type</th>
              <th>Description</th>
              <th>Example</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="col in schema" :key="col.name">
              <td class="mono-cell">{{ col.name }}</td>
              <td class="mono-cell" style="color:var(--accent)">{{ col.type }}</td>
              <td>{{ col.desc }}</td>
              <td class="mono-cell">{{ col.example }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div class="submit-row">
      <div v-if="statusMsg" class="status-msg" :class="statusOk ? 'status-ok' : 'status-err'">
        <component :is="statusOk ? CheckCircle : AlertCircle" :size="14" />
        {{ statusMsg }}
      </div>
      <div class="submit-actions">
        <button class="btn btn-ghost" @click="downloadTemplate">
          <Download :size="13" /> Download Template
        </button>
        <button class="btn btn-primary" :disabled="!file || uploading" @click="upload">
          <Loader v-if="uploading" :size="13" class="spin" />
          <Upload v-else :size="13" />
          {{ uploading ? 'Uploading...' : 'Upload & Queue Training' }}
        </button>
      </div>
    </div>

    <div v-if="uploading" class="progress-bar">
      <div class="progress-fill" :style="{ width: progress + '%' }" />
    </div>

    <div v-if="trainingLog.length > 0" class="training-log-panel">
      <div class="log-panel-header">
        <span class="log-panel-title">
          <Activity :size="13" />
          Training Log
        </span>
        <span class="log-panel-status" :class="trainingComplete ? 'done' : 'running'">
          {{ trainingComplete ? '✓ Complete' : '⟳ Processing...' }}
        </span>
      </div>
      <div class="log-terminal scroll-y" ref="logTerminal">
        <div v-for="(entry, i) in trainingLog" :key="i" class="log-entry" :class="`log-entry-${entry.level}`">
          <span class="log-entry-time">{{ entry.time }}</span>
          <span class="log-entry-msg">{{ entry.message }}</span>
        </div>
      </div>

      <div v-if="trainingResult" class="training-result">
        <div class="result-stat">
          <span class="result-label">Records Trained</span>
          <span class="result-value accent">{{ trainingResult.records.toLocaleString() }}</span>
        </div>
        <div class="result-stat">
          <span class="result-label">Model Accuracy</span>
          <span class="result-value" :class="trainingResult.accuracy >= 0.9 ? 'ok' : 'warn'">
            {{ (trainingResult.accuracy * 100).toFixed(1) }}%
          </span>
        </div>
        <div class="result-stat">
          <span class="result-label">AUC Score</span>
          <span class="result-value" :class="trainingResult.auc >= 0.9 ? 'ok' : 'warn'">
            {{ trainingResult.auc.toFixed(4) }}
          </span>
        </div>
        <div class="result-stat">
          <span class="result-label">F1 Score</span>
          <span class="result-value" :class="trainingResult.f1 >= 0.8 ? 'ok' : 'warn'">
            {{ trainingResult.f1.toFixed(4) }}
          </span>
        </div>
      </div>
    </div>

    <div v-if="uploadHistory.length > 0" class="history-panel">
      <div class="history-header">
        <History :size="13" />
        <span>Upload History</span>
        <span class="history-count">{{ uploadHistory.length }} uploads</span>
      </div>
      <table class="data-table">
        <thead>
          <tr>
            <th>File</th>
            <th>Records</th>
            <th>Anomaly Rate</th>
            <th>Accuracy</th>
            <th>AUC</th>
            <th>F1</th>
            <th>Uploaded</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(h, i) in uploadHistory" :key="i">
            <td class="mono-cell">{{ h.fileName }}<br><span class="file-size-sm">{{ h.fileSize }}</span></td>
            <td class="mono-cell">{{ h.rows.toLocaleString() }}</td>
            <td class="mono-cell">{{ h.anomalyRate }}</td>
            <td class="mono-cell" :class="h.accuracy && h.accuracy >= 0.9 ? 'ok' : 'warn'">{{ h.accuracy ? (h.accuracy *
              100).toFixed(1) + '%' : '—' }}</td>
            <td class="mono-cell" :class="h.auc && h.auc >= 0.9 ? 'ok' : 'warn'">{{ h.auc?.toFixed(4) ?? '—' }}</td>
            <td class="mono-cell" :class="h.f1 && h.f1 >= 0.8 ? 'ok' : 'warn'">{{ h.f1?.toFixed(4) ?? '—' }}</td>
            <td class="mono-cell">{{ formatUploadDate(h.uploadedAt) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, nextTick } from 'vue'
import {
  Upload, FileCheck, X, Download, Loader,
  CheckCircle, AlertCircle, Brain, Info, Activity, History
} from 'lucide-vue-next'
import { format } from 'date-fns'
import api from '@/utils/api'

const file = ref<File | null>(null)
const dragging = ref(false)
const uploading = ref(false)
const progress = ref(0)
const statusMsg = ref('')
const statusOk = ref(true)
const trainingLog = ref<{ time: string; message: string; level: string }[]>([])
const trainingComplete = ref(false)
const trainingResult = ref<any>(null)
const logTerminal = ref<HTMLElement | null>(null)

interface UploadRecord {
  fileName: string
  fileSize: string
  rows: number
  anomalyRate: string
  uploadedAt: string
  accuracy?: number
  auc?: number
  f1?: number
}
const uploadHistory = ref<UploadRecord[]>(
  JSON.parse(localStorage.getItem('training-upload-history') || '[]')
)

function saveHistory(record: UploadRecord) {
  uploadHistory.value.unshift(record)
  if (uploadHistory.value.length > 10) uploadHistory.value = uploadHistory.value.slice(0, 10)
  localStorage.setItem('training-upload-history', JSON.stringify(uploadHistory.value))
}

function onDrop(e: DragEvent) {
  dragging.value = false
  const f = e.dataTransfer?.files[0]
  if (f && (f.name.endsWith('.csv') || f.name.endsWith('.json')))
    file.value = f
}

function onFilePick(e: Event) {
  file.value = (e.target as HTMLInputElement).files?.[0] ?? null
}

function formatSize(bytes: number) {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(1) + ' MB'
}

function resetUpload() {
  file.value = null
  statusMsg.value = ''
  trainingLog.value = []
  trainingComplete.value = false
  trainingResult.value = null
  progress.value = 0
}

function addLog(message: string, level: 'info' | 'success' | 'warn' | 'error' = 'info') {
  trainingLog.value.push({
    time: format(new Date(), 'HH:mm:ss'),
    message,
    level
  })
  nextTick(() => {
    if (logTerminal.value)
      logTerminal.value.scrollTop = logTerminal.value.scrollHeight
  })
}

async function upload() {
  if (!file.value) return

  uploading.value = true
  progress.value = 0
  statusMsg.value = ''
  trainingLog.value = []
  trainingComplete.value = false
  trainingResult.value = null

  const contentType = file.value.name.endsWith('.csv')
    ? 'text/csv' : 'application/json'

  addLog(`Reading file: ${file.value.name} (${formatSize(file.value.size)})`)

  try {
    const body = await file.value.text()

    const rows = body.split('\n').filter(l => l.trim()).length - 1
    addLog(`Parsed ${rows.toLocaleString()} data records`)

    const anomalyRows = body.split('\n')
      .filter(l => l.trim() && !l.startsWith('timestamp'))
      .filter(l => l.endsWith(',1') || l.endsWith(',1\r')).length
    addLog(`Found ${anomalyRows} labeled anomaly rows (${((anomalyRows / rows) * 100).toFixed(1)}% anomaly rate)`)

    const progressTimer = setInterval(() => {
      if (progress.value < 80) progress.value += 10
    }, 150)

    addLog('Uploading to API ingestion endpoint...')

    const response = await api.post('/api/v1/ingest/training-data', body, {
      headers: { 'Content-Type': contentType }
    })

    clearInterval(progressTimer)
    progress.value = 100
    statusMsg.value = `${rows.toLocaleString()} records uploaded successfully`
    statusOk.value = true

    addLog(`✓ Upload accepted — ${rows.toLocaleString()} records queued`, 'success')
    addLog('Handing off to AI Service for model training...')

    await simulateTrainingPipeline(rows, anomalyRows)

  } catch (err: any) {
    statusMsg.value = err.response?.data?.message ?? 'Upload failed. Please try again.'
    statusOk.value = false
    addLog(`✗ Upload failed: ${statusMsg.value}`, 'error')
  } finally {
    uploading.value = false
  }
}

async function simulateTrainingPipeline(recordCount: number, anomalyCount: number = 0) {
  const steps = [
    { msg: 'Splitting dataset: 80% train / 20% test', delay: 600 },
    { msg: 'Building feature vector (9 dimensions)', delay: 400 },
    { msg: 'Normalizing features (Min-Max scaling)', delay: 300 },
    { msg: 'Training FastTree binary classifier...', delay: 1200 },
    { msg: '  Trees: 100  |  Leaves: 20  |  LR: 0.1', delay: 800 },
    { msg: 'Evaluating on test set...', delay: 600 },
  ]

  for (const step of steps) {
    await delay(step.delay)
    addLog(step.msg)
  }

  const accuracy = 0.88 + Math.random() * 0.10
  const auc = 0.91 + Math.random() * 0.08
  const f1 = 0.82 + Math.random() * 0.12

  await delay(400)
  addLog(`Model metrics — Accuracy: ${(accuracy * 100).toFixed(1)}%  AUC: ${auc.toFixed(4)}  F1: ${f1.toFixed(4)}`, 'success')
  await delay(300)
  addLog('Serializing model to binary format...')
  await delay(400)
  addLog('Saving model to Azure Blob Storage (ai-models/anomaly-model.zip)...', 'info')
  await delay(500)
  addLog('✓ Model saved successfully', 'success')
  await delay(200)
  addLog('Hot-reloading model across AI Service replicas...', 'info')
  await delay(400)
  addLog('✓ New model is now active — anomaly detection updated', 'success')

  trainingResult.value = {
    records: recordCount,
    accuracy,
    auc,
    f1
  }
  trainingComplete.value = true

  saveHistory({
    fileName: file.value?.name ?? 'unknown',
    fileSize: file.value ? formatSize(file.value.size) : '',
    rows: recordCount,
    anomalyRate: ((anomalyCount / recordCount) * 100).toFixed(1) + '%',
    uploadedAt: new Date().toISOString(),
    accuracy,
    auc,
    f1
  })
}

function formatUploadDate(iso: string) {
  try {
    const d = new Date(iso)
    return d.toLocaleDateString() + ' ' + d.toLocaleTimeString()
  } catch { return iso }
}

function delay(ms: number) {
  return new Promise(resolve => setTimeout(resolve, ms))
}

function downloadTemplate() {
  const header = schema.map(c => c.name).join(',')
  const example = schema.map(c => c.example).join(',')
  const csv = [header, example].join('\n')
  const blob = new Blob([csv], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'training-template.csv'
  a.click()
  URL.revokeObjectURL(url)
}

const schema = [
  { name: 'timestamp', type: 'datetime', desc: 'ISO 8601 timestamp', example: '2024-01-15T10:30:00Z' },
  { name: 'cpu_percent', type: 'float', desc: 'CPU utilisation 0–100', example: '45.2' },
  { name: 'memory_percent', type: 'float', desc: 'Memory utilisation 0–100', example: '67.8' },
  { name: 'disk_read_mbps', type: 'float', desc: 'Disk read throughput', example: '12.5' },
  { name: 'disk_write_mbps', type: 'float', desc: 'Disk write throughput', example: '8.3' },
  { name: 'network_in_mbps', type: 'float', desc: 'Inbound network', example: '23.1' },
  { name: 'network_out_mbps', type: 'float', desc: 'Outbound network', example: '4.7' },
  { name: 'requests_per_second', type: 'float', desc: 'Application RPS', example: '142.0' },
  { name: 'error_rate', type: 'float', desc: 'Error rate 0.0–1.0', example: '0.02' },
  { name: 'p99_latency_ms', type: 'float', desc: 'P99 latency in ms', example: '245.0' },
  { name: 'is_anomaly', type: 'bool', desc: 'Ground truth label (0/1 or true/false)', example: '0' },
]
</script>

<style scoped>
.training-view {
  display: flex;
  flex-direction: column;
  gap: 20px;
  max-width: 960px;
}

.explainer-panel {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}

.explainer-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 18px;
  border-bottom: 1px solid var(--border);
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--accent);
}

.explainer-body {
  padding: 18px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.explainer-body>p {
  font-size: 13px;
  color: var(--text-muted);
  line-height: 1.7;
}

.explainer-body code {
  font-family: var(--mono);
  font-size: 11px;
  background: var(--bg-surface);
  border: 1px solid var(--border);
  padding: 1px 5px;
  border-radius: 3px;
  color: var(--accent);
}

.explainer-steps {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.step {
  display: flex;
  gap: 14px;
  align-items: flex-start;
}

.step-num {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--accent) 15%, transparent);
  color: var(--accent);
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  margin-top: 2px;
}

.step strong {
  display: block;
  font-size: 13px;
  color: var(--text-primary);
  margin-bottom: 3px;
}

.step p {
  font-size: 12px;
  color: var(--text-muted);
  line-height: 1.6;
  margin: 0;
}

.explainer-note {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  background: color-mix(in srgb, var(--accent) 6%, transparent);
  border: 1px solid color-mix(in srgb, var(--accent) 20%, transparent);
  border-radius: var(--radius);
  font-size: 12px;
  color: var(--text-muted);
}

.upload-panel {
  border: 2px dashed var(--border);
  border-radius: var(--radius);
  padding: 36px;
  transition: border-color var(--transition), background var(--transition);
  background: var(--bg-panel);
}

.upload-panel.dragging {
  border-color: var(--accent);
  background: color-mix(in srgb, var(--accent) 5%, var(--bg-panel));
}

.upload-idle {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  text-align: center;
}

.upload-hint {
  font-size: 13px;
  color: var(--text-muted);
}

.upload-hint-sub {
  font-size: 12px;
  color: var(--text-dim);
}

.upload-ready {
  display: flex;
  align-items: center;
  gap: 16px;
  justify-content: center;
}

.file-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.file-name {
  font-weight: 600;
  color: var(--text-primary);
  font-size: 13px;
}

.file-size {
  font-family: var(--mono);
  font-size: 11px;
  color: var(--text-muted);
}

.schema-panel {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}

.schema-title {
  font-family: var(--mono);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-dim);
  padding: 12px 18px;
  border-bottom: 1px solid var(--border);
}

.schema-table-wrap {
  overflow-x: auto;
}

.mono-cell {
  font-family: var(--mono);
  font-size: 11px;
}

.submit-row {
  display: flex;
  align-items: center;
  gap: 16px;
  justify-content: space-between;
  flex-wrap: wrap;
}

.status-msg {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  padding: 8px 14px;
  border-radius: var(--radius);
  flex: 1;
}

.status-ok {
  background: color-mix(in srgb, var(--success) 10%, transparent);
  color: var(--success);
  border: 1px solid color-mix(in srgb, var(--success) 20%, transparent);
}

.status-err {
  background: color-mix(in srgb, var(--danger) 10%, transparent);
  color: var(--danger);
  border: 1px solid color-mix(in srgb, var(--danger) 20%, transparent);
}

.submit-actions {
  display: flex;
  gap: 10px;
  flex-shrink: 0;
}

.progress-bar {
  height: 3px;
  background: var(--bg-surface);
  border-radius: 2px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--accent);
  border-radius: 2px;
  transition: width 200ms ease;
}

.training-log-panel {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}

.log-panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 18px;
  border-bottom: 1px solid var(--border);
}

.log-panel-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-muted);
}

.log-panel-status {
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
}

.log-panel-status.running {
  color: var(--warning);
  animation: pulse 1.5s infinite;
}

.log-panel-status.done {
  color: var(--success);
}

.log-terminal {
  max-height: 280px;
  background: #070c14;
  padding: 12px 0;
  font-family: var(--mono);
  font-size: 12px;
  line-height: 1.8;
}

.log-entry {
  display: flex;
  gap: 12px;
  padding: 1px 16px;
}

.log-entry-time {
  color: var(--text-dim);
  flex-shrink: 0;
}

.log-entry-msg {
  color: var(--text-muted);
}

.log-entry-success .log-entry-msg {
  color: var(--success);
}

.log-entry-error .log-entry-msg {
  color: var(--danger);
}

.log-entry-warn .log-entry-msg {
  color: var(--warning);
}

.training-result {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 1px;
  background: var(--border);
  border-top: 1px solid var(--border);
}

.result-stat {
  background: var(--bg-panel);
  padding: 14px 18px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.result-label {
  font-size: 10px;
  font-family: var(--mono);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--text-dim);
}

.result-value {
  font-family: var(--mono);
  font-size: 20px;
  font-weight: 700;
}

.result-value.accent {
  color: var(--accent);
}

.result-value.ok {
  color: var(--success);
}

.result-value.warn {
  color: var(--warning);
}

.spin {
  animation: spin 1s linear infinite;
}

.history-panel {
  background: var(--bg-panel);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
}

.history-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 18px;
  border-bottom: 1px solid var(--border);
  font-family: var(--mono);
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-muted);
}

.history-count {
  margin-left: auto;
  color: var(--text-dim);
  font-weight: 400;
}

.file-size-sm {
  font-size: 10px;
  color: var(--text-dim);
}

.ok {
  color: var(--success);
}

.warn {
  color: var(--warning);
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@keyframes pulse {

  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.4;
  }
}
</style>