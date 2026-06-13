import type { RouteLocationNormalized, Router } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { usePermissionStore } from '@/stores/permission'
import { getPageTitle } from '@/utils/get-page-title'
import { generateDynamicRoutes } from './modules/system'

const whiteList = ['/login', '/403', '/404']

/** 设置up Permission Guard */
export function setupPermissionGuard(router: Router) {
  router.beforeEach(async (to: RouteLocationNormalized, _from, next) => {
    const auth = useAuthStore()

    document.title = getPageTitle((to.meta?.title as string) || '')

    if (to.path === '/login' && auth.isLoggedIn) {
      return next('/')
    }

    if (whiteList.includes(to.path)) {
      return next()
    }

    if (!auth.isLoggedIn) {
      return next(`/login?redirect=${to.path}`)
    }

    if (!auth.user) {
      try {
        await auth.fetchCurrentUser()
      }
      catch {
        auth.logoutAction()
        return next('/login')
      }
    }

    const permission = usePermissionStore()

    // 动态路由尚未注册 → 拉取菜单树并注册路由
    if (!permission.routesAdded) {
      try {
        if (!permission.menuLoaded) {
          await permission.fetchMenus()
        }
        const dynamicRoutes = generateDynamicRoutes(permission.menuTree)
        for (const route of dynamicRoutes) {
          router.addRoute(route)
        }
        // catch-all 必须在所有动态路由之后注册
        router.addRoute({
          path: '/:pathMatch(.*)*',
          name: 'NotFound',
          component: () => import('@/views/404.vue'),
          meta: { title: '页面不存在', hidden: true },
        })
        permission.markRoutesAdded()
        return next({ ...to, replace: true })
      }
      catch {
        return next('/login')
      }
    }

    next()
  })
}
