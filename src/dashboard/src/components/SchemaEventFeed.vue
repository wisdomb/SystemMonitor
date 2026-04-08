<template>
    <div class="schema-feed">
        <div v-if="displayList.length === 0" class="feed-empty">
            No attribute changes detected yet
        </div>
        <RouterLink v-for="evt in displayList" :key="evt.id" to="/anomalies" class="feed-row" :class="rowClass(evt)">
            <span class="row-dot" />
            <div class="row-info">
                <span class="row-raw">{{ evt.rawAttribute }}</span>
                <span v-if="evt.wasResolved" class="row-resolved">
                    → {{ evt.resolvedAttribute }}
                </span>
                <span v-else class="row-unresolved">→ unresolved</span>
            </div>
            <div class="row-meta">
                <span class="row-tier">{{ evt.resolutionTier }}</span>
                <span class="row-conf" v-if="evt.wasResolved">
                    {{ (evt.confidence * 100).toFixed(0) }}%
                </span>
            </div>
        </RouterLink>
    </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { SchemaResolutionEvent } from '@/types'

const props = withDefaults(defineProps<{
    events: SchemaResolutionEvent[]
    max?: number
}>(), { max: 5 })

const displayList = computed(() =>
    [...props.events]
        .sort((a, b) => new Date(b.detectedAt).getTime() - new Date(a.detectedAt).getTime())
        .slice(0, props.max)
)

function rowClass(evt: SchemaResolutionEvent) {
    if (!evt.wasResolved) return 'row-critical'
    if (evt.confidence < 0.60) return 'row-critical'
    if (evt.confidence < 0.75) return 'row-high'
    if (evt.confidence < 0.90) return 'row-medium'
    return 'row-low'
}
</script>

<style scoped>
.schema-feed {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.feed-empty {
    padding: 24px;
    text-align: center;
    font-size: 12px;
    color: var(--text-dim);
}

.feed-row {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 10px;
    border-radius: 6px;
    background: var(--bg-surface);
    border-left: 2px solid transparent;
    text-decoration: none;
    cursor: pointer;
    transition: background var(--transition);
}

.feed-row:hover {
    background: var(--bg-hover);
}

.row-critical {
    border-left-color: var(--danger);
}

.row-high {
    border-left-color: var(--warning);
}

.row-medium {
    border-left-color: var(--accent);
}

.row-low {
    border-left-color: var(--text-dim);
}

.row-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    flex-shrink: 0;
}

.row-critical .row-dot {
    background: var(--danger);
    box-shadow: 0 0 6px var(--danger);
}

.row-high .row-dot {
    background: var(--warning);
}

.row-medium .row-dot {
    background: var(--accent);
}

.row-low .row-dot {
    background: var(--text-dim);
}

.row-info {
    flex: 1;
    min-width: 0;
    display: flex;
    align-items: center;
    gap: 6px;
    flex-wrap: wrap;
}

.row-raw {
    font-family: var(--mono);
    font-size: 11px;
    font-weight: 700;
    color: var(--text-primary);
}

.row-resolved {
    font-family: var(--mono);
    font-size: 11px;
    color: var(--text-muted);
}

.row-unresolved {
    font-family: var(--mono);
    font-size: 11px;
    color: var(--danger);
}

.row-meta {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 2px;
    flex-shrink: 0;
}

.row-tier {
    font-family: var(--mono);
    font-size: 10px;
    color: var(--text-dim);
}

.row-conf {
    font-family: var(--mono);
    font-size: 11px;
    font-weight: 700;
    color: var(--text-muted);
}
</style>