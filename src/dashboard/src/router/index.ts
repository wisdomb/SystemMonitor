import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'overview',
      component: () => import('@/views/OverviewView.vue'),
      meta: { title: 'Overview' }
    },
    {
      path: '/anomalies',
      name: 'anomalies',
      component: () => import('@/views/AnomaliesView.vue'),
      meta: { title: 'Anomaly Detection' }
    },
    {
      path: '/metrics',
      name: 'metrics',
      component: () => import('@/views/MetricsView.vue'),
      meta: { title: 'Metrics Explorer' }
    },
    {
      path: '/agents',
      name: 'agents',
      component: () => import('@/views/AgentsView.vue'),
      meta: { title: 'Agent Fleet' }
    },
    {
      path: '/logs',
      name: 'logs',
      component: () => import('@/views/LogsView.vue'),
      meta: { title: 'Log Stream' }
    },
    {
      path: '/infra',
      name: 'infra',
      component: () => import('@/views/InfraView.vue'),
      meta: { title: 'Infrastructure' }
    },
    {
      path: '/schema',
      name: 'schema',
      component: () => import('@/views/SchemaView.vue'),
      meta: { title: 'Schema Registry' }
    },
    {
      path: '/training',
      name: 'training',
      component: () => import('@/views/TrainingView.vue'),
      meta: { title: 'Training Data' }
    }
  ]
})

router.afterEach(to => {
  document.title = `${to.meta.title ?? 'Monitor'} — Dashboard`
})

export default router
