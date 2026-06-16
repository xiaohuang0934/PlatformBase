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
    path: '/m/dashboard',
    name: 'MobileDashboard',
    component: () => import('@/views/dashboard/MobileDashboard.vue'),
    meta: { title: '工作台', hidden: true, keepAlive: true },
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
    component: () => import('@/views/apps/MobileApps.vue'),
    meta: { title: '应用中心', hidden: true, keepAlive: true },
  },
  {
    path: '/m/notifications',
    name: 'MobileNotifications',
    component: () => import('@/views/notifications/MobileNotifications.vue'),
    meta: { title: '消息通知', hidden: true, keepAlive: true },
  },
  {
    path: '/m/profile',
    name: 'MobileProfile',
    component: () => import('@/views/profile/MobileProfile.vue'),
    meta: { title: '个人中心', hidden: true },
  },
  // 移动端用户 CRUD 路由
  {
    path: '/m/users/create',
    name: 'MobileUserCreate',
    component: () => import('@/views/users/MobileUserEdit.vue'),
    meta: { title: '新增用户', hidden: true },
  },
  {
    path: '/m/users/:id/edit',
    name: 'MobileUserEdit',
    component: () => import('@/views/users/MobileUserEdit.vue'),
    meta: { title: '编辑用户', hidden: true },
  },
  {
    path: '/m/users/:id',
    name: 'MobileUserDetail',
    component: () => import('@/views/users/MobileUserDetail.vue'),
    meta: { title: '用户详情', hidden: true },
  },
  {
    path: '/m/roles/:id',
    name: 'MobileRoleDetail',
    component: () => import('@/views/roles/MobileRoleDetail.vue'),
    meta: { title: '角色详情', hidden: true },
  },
  {
    path: '/m/permissions/:id',
    name: 'MobilePermissionDetail',
    component: () => import('@/views/permissions/MobilePermissionDetail.vue'),
    meta: { title: '权限详情', hidden: true },
  },
  {
    path: '/m/tenants/:id',
    name: 'MobileTenantDetail',
    component: () => import('@/views/tenants/MobileTenantDetail.vue'),
    meta: { title: '租户详情', hidden: true },
  },
  {
    path: '/m/system-params/:id',
    name: 'MobileSystemParamDetail',
    component: () => import('@/views/system-params/MobileSystemParamDetail.vue'),
    meta: { title: '参数详情', hidden: true },
  },
  {
    path: '/m/data-dict/:id',
    name: 'MobileDataDictTypeDetail',
    component: () => import('@/views/data-dict/MobileDataDictTypeDetail.vue'),
    meta: { title: '字典详情', hidden: true },
  },
  {
    path: '/m/organization-units/:id',
    name: 'MobileOrganizationUnitDetail',
    component: () => import('@/views/organization-units/MobileOrganizationUnitDetail.vue'),
    meta: { title: '组织详情', hidden: true },
  },
  {
    path: '/m/menus/create',
    name: 'MobileMenuCreate',
    component: () => import('@/views/menus/MobileMenuEdit.vue'),
    meta: { title: '新增菜单', hidden: true },
  },
  {
    path: '/m/menus/:id/edit',
    name: 'MobileMenuEdit',
    component: () => import('@/views/menus/MobileMenuEdit.vue'),
    meta: { title: '编辑菜单', hidden: true },
  },
  {
    path: '/m/menus/:parentId/children',
    name: 'MobileMenuChildren',
    component: () => import('@/views/menus/MobileMenuChildren.vue'),
    meta: { title: '子菜单', hidden: true },
  },
]
