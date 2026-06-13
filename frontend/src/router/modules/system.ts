import type { RouteRecordRaw } from 'vue-router'
import type { MenuDto } from '@/types/auth'

/** 将菜单树转换为Vue Router路由表 */
export function generateDynamicRoutes(menus: MenuDto[]): RouteRecordRaw[] {
  const result: RouteRecordRaw[] = []

  /** Walk */
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

/** 路径片段转合法路由名（kebab→Pascal） */
function toRouteName(module: string): string {
  return module
    .split('-')
    .map(p => p.charAt(0).toUpperCase() + p.slice(1))
    .join('')
}

/** 按约定路径解析视图组件 */
function resolveComponent(module: string) {
  const pascal = toRouteName(module)
  return () => {
    const isMobile = typeof window !== 'undefined' && window.innerWidth < 768
    const prefix = isMobile ? 'Mobile' : 'Desktop'
    return import(`@/views/${module}/${prefix}${pascal}List.vue`).catch(() =>
      import('@/views/Placeholder.vue'),
    )
  }
}
