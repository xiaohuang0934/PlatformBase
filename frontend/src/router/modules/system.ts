import type { RouteRecordRaw } from 'vue-router'
import type { MenuDto } from '@/types/auth'

/** 已知模块 → 组件映射表，用于精确路由到视图文件 */
const componentMap: Record<string, () => Promise<unknown>> = {
  'users': () => import('@/views/users/DesktopUsersList.vue'),
  'roles': () => import('@/views/roles/DesktopRolesList.vue'),
  'permissions': () => import('@/views/permissions/DesktopPermissionsList.vue'),
  'menus': () => import('@/views/menus/DesktopMenusList.vue'),
  'tenants': () => import('@/views/tenants/DesktopTenantsList.vue'),
  'organization-units': () => import('@/views/organization-units/DesktopOrganizationUnitsList.vue'),
  'system-params': () => import('@/views/system-params/DesktopSystemParamsList.vue'),
  'data-dict': () => import('@/views/data-dict/DesktopDataDictList.vue'),
  'operation-logs': () => import('@/views/operation-logs/DesktopOperationLogsList.vue'),
  'files': () => import('@/views/files/DesktopFilesList.vue'),
  'jobs': () => import('@/views/jobs/DesktopJobsList.vue'),
  'import-export': () => import('@/views/import-export/DesktopImportExportList.vue'),
  'notifications': () => import('@/views/notifications/DesktopNotificationsList.vue'),
}

export function generateDynamicRoutes(menus: MenuDto[]): RouteRecordRaw[] {
  const result: RouteRecordRaw[] = []

  function walk(list: MenuDto[]) {
    for (const menu of list) {
      if (menu.path) {
        const module = menu.path.replace(/^\//, '')
        result.push({
          path: menu.path,
          name: toRouteName(module),
          component: resolveComponent(module),
          meta: {
            title: menu.name,
            icon: menu.icon,
            permissionCode: menu.permissionCode,
            keepAlive: menu.keepAlive ?? true,
          },
        })
      }
      if (menu.children?.length)
        walk(menu.children)
    }
  }

  walk(menus)
  return result
}

/** 将路径片段转为合法的路由 name（如 users→Users、system-params→SystemParams） */
function toRouteName(module: string): string {
  return module
    .split('-')
    .map(p => p.charAt(0).toUpperCase() + p.slice(1))
    .join('')
}

function resolveComponent(module: string) {
  if (module in componentMap) {
    return componentMap[module]
  }
  const pascal = toRouteName(module)
  return () =>
    import(`@/views/${module}/Desktop${pascal}List.vue`).catch(() =>
      import('@/views/Placeholder.vue'),
    )
}
