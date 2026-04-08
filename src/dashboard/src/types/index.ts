export interface MetricEvent {
  id: string
  agentId: string
  hostName: string
  environment: string
  timestamp: string
  type: MetricType
  values: Record<string, number>
  tags: Record<string, string>
}

export interface LogEvent {
  id: string
  agentId: string
  hostName: string
  serviceName: string
  environment: string
  timestamp: string
  level: LogLevel
  message: string
  stackTrace?: string
  properties: Record<string, string>
}

export interface AnomalyResult {
  id: string
  sourceEventId: string
  agentId: string
  hostName: string
  detectedAt: string
  isAnomaly: boolean
  confidence: number
  type: AnomalyType
  severity: AnomalySeverity
  description: string
  affectedMetrics: Record<string, number>
}

export interface HealthScore {
  agentId: string
  score: number
  timestamp: string
}

export interface DashboardSummary {
  totalAgents: number
  activeAgents: number
  anomaliesLast1h: number
  criticalAlerts: number
  ingestionRatePerMin: number
  avgHealthScore: number
  errorRatePercent: number
  avgLatencyMs: number
}

export interface InfrastructureStatus {
  metricQueueDepth: number
  logQueueDepth: number
  workerCount: number
  processingDelayMs: number
  cosmosRequestUnits: number
}

export interface TimeSeriesPoint {
  timestamp: string
  value: number
}

export enum MetricType {
  System = 'System',
  Application = 'Application',
  Network = 'Network',
  Database = 'Database',
  Custom = 'Custom'
}

export enum LogLevel {
  Trace = 'Trace',
  Debug = 'Debug',
  Information = 'Information',
  Warning = 'Warning',
  Error = 'Error',
  Critical = 'Critical'
}

export enum AnomalyType {
  Spike = 'Spike',
  Drop = 'Drop',
  ChangePoint = 'ChangePoint',
  Pattern = 'Pattern',
  Correlation = 'Correlation'
}

export enum AnomalySeverity {
  Low = 'Low',
  Medium = 'Medium',
  High = 'High',
  Critical = 'Critical'
}

export interface SchemaResolutionEvent {
  id: string
  agentId: string
  hostName: string
  rawAttribute: string
  resolvedAttribute: string | null
  confidence: number
  resolutionTier: string
  wasResolved: boolean
  detectedAt: string
}