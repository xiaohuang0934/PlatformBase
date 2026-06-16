import type { RouteRecordRaw } from 'vue-router'
import type { MenuDto } from '@/types/auth'

/** 将菜单树转换为Vue Router路由表 */
export function generateDynamicRoutes(menus: MenuDto[], isMobile: boolean = false): RouteRecordRaw[] {
  const result: RouteRecordRaw[] = []

  /** Walk */
  function walk(list: MenuDto[]) {
    for (const menu of list) {
      if (menu.path) {
        const module = menu.path.replace(/^\//, '')
        const path = isMobile ? `/m${menu.path}` : menu.path

        result.push({
          path,
          name: isMobile ? `Mobile${toRouteName(module)}` : toRouteName(module),
          component: resolveComponent(module, isMobile),
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

/** 路径片段转合法路由名（kebab→Pascal） */
function toRouteName(module: string): string {
  return module
    .split('-')
    .map(p => p.charAt(0).toUpperCase() + p.slice(1))
    .join('')
}

/** 按约定路径解析视图组件 */
function resolveComponent(module: string, isMobile: boolean) {
  const pascal = toRouteName(module)
  const prefix = isMobile ? 'Mobile' : 'Desktop'

  // 移动端可能没有 List 后缀，需要尝试多种命名
  if (isMobile) {
    return () => import(`@/views/${module}/${prefix}${pascal}List.vue`)
      .catch(() => import(`@/views/${module}/${prefix}${pascal}.vue`))
      .catch(() => import('@/views/Placeholder.vue'))
  }

  // 桌面端统一使用 List 后缀
  return () => import(`@/views/${module}/${prefix}${pascal}List.vue`).catch(() =>
    import('@/views/Placeholder.vue'),
  )
}
