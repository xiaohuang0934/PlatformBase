import type { RouteRecordRaw } from 'vue-router'
import type { MenuDto } from '@/types/auth'

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

function toRouteName(module: string): string {
  return module
    .split('-')
    .map(p => p.charAt(0).toUpperCase() + p.slice(1))
    .join('')
}

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
