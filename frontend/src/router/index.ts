import { createRouter, createWebHistory } from 'vue-router'
import { constantRoutes } from './modules/auth'
import { setupPermissionGuard } from './permission'

const router = createRouter({
  history: createWebHistory(),
  routes: constantRoutes,
})

setupPermissionGuard(router)

export default router
