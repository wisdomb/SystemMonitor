<template>
  <div class="app-shell">
    <SideNav />
    <div class="main-content">
      <TopBar />
      <main class="page-body">
        <router-view v-slot="{ Component }">
          <transition name="fade">
            <component :is="Component" />
          </transition>
        </router-view>
      </main>
    </div>
    <AlertToast />
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import SideNav    from '@/components/SideNav.vue'
import TopBar     from '@/components/TopBar.vue'
import AlertToast from '@/components/AlertToast.vue'
import { useMonitoringStore } from '@/stores/monitoring'
import { useSignalR } from '@/composables/useSignalR'
import { useIntervalFn } from '@vueuse/core'

const store = useMonitoringStore()
useSignalR()

onMounted(() => store.initialize())
useIntervalFn(() => store.refresh(), 10_000)
</script>

<style>
.app-shell {
  display: flex;
  height: 100vh;
  overflow: hidden;
}

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.page-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
  scrollbar-width: thin;
  scrollbar-color: var(--border) transparent;
}
</style>