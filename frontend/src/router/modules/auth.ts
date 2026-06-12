import type { RouteRecordRaw } from 'vue-router'

/** 静态路由 — 不依赖权限 */
export const constantRoutes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/dashboard',
  },
  {
    path: '/redirect',
    component: () => import('@/views/redirect/index.vue'),
    meta: { hidden: true },
  },
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/login/index.vue'),
    meta: { title: '登录', noAuth: true },
  },
  {
    path: '/dashboard',
    name: 'Dashboard',
    component: () => import('@/views/dashboard/DesktopDashboard.vue'),
    meta: { title: '工作台', icon: 'DataAnalysis', affix: true, keepAlive: true },
  },
  {
    path: '/403',
    name: 'Forbidden',
    component: () => import('@/views/403.vue'),
    meta: { title: '无权限', hidden: true },
  },
  {
    path: '/404',
    name: 'NotFoundPage',
    component: () => import('@/views/404.vue'),
    meta: { title: '页面不存在', hidden: true },
  },
  {
    path: '/m/apps',
    name: 'MobileApps',
    component: () => import('@/views/Placeholder.vue'),
    meta: { title: '应用中心', hidden: true },
  },
  {
    path: '/m/notifications',
    name: 'MobileNotifications',
    component: () => import('@/views/notifications/DesktopNotificationsList.vue'),
    meta: { title: '消息通知', hidden: true, keepAlive: true },
  },
  {
    path: '/m/profile',
    name: 'MobileProfile',
    component: () => import('@/views/Placeholder.vue'),
    meta: { title: '个人中心', hidden: true },
  },
]
