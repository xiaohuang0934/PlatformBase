import type { LoginRequest, UserProfileDto } from '@/types/auth'
import { UserType } from '@/types/user'
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import * as authApi from '@/api/auth'
import { getAccessibleTenants } from '@/api/tenants'
import {
  clearTokens,
  setAccessToken,
  setRefreshToken,
} from '@/utils/token'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>(localStorage.getItem('accessToken') || '')
  const user = ref<UserProfileDto | null>(null)
  const permissionCodes = ref<string[]>([])
  const tenantIds = ref<string[]>([])
  const currentTenantId = ref<string | null>(null)

  const isLoggedIn = computed(() => !!token.value)
  const isPlatformAdmin = computed(() => user.value?.userType === UserType.PlatformAdmin)
  const isSuperAdmin = computed(() => isPlatformAdmin.value)
  const roles = computed(() => user.value?.roles ?? [])
  const displayName = computed(() => user.value?.username ?? '')

  /** 登录：获取Token并持久化 */
  async function loginAction(data: LoginRequest) {
    const res = await authApi.login(data)
    const { accessToken, refreshToken, expiresIn } = res.data

    setAccessToken(accessToken, expiresIn)
    if (refreshToken) {
      setRefreshToken(refreshToken)
    }

    token.value = accessToken
  }

  /** 获取当前用户信息 + 权限码 + 可访问租户列表 */
  async function fetchCurrentUser() {
    try {
      const [profileRes, permsRes] = await Promise.all([
        authApi.getProfile(),
        authApi.getPermissions(),
      ])
      user.value = profileRes.data
      permissionCodes.value = permsRes.data

      if (profileRes.data.userType === UserType.PlatformAdmin) {
        try {
          const tenantRes = await getAccessibleTenants()
          tenantIds.value = (tenantRes.data || []).map((t: any) => t.id)
        }
        catch {
          tenantIds.value = []
        }
      }
    }
    catch {
      logoutAction()
      window.location.href = '/login'
    }
  }

  /** 刷新可访问租户列表 */
  async function refreshTenantIds() {
    if (user.value?.userType !== UserType.PlatformAdmin)
      return
    try {
      const tenantRes = await getAccessibleTenants()
      tenantIds.value = (tenantRes.data || []).map((t: any) => t.id)
    }
    catch { /* 静默失败 */ }
  }

  /** 切换当前租户视角 */
  function setCurrentTenantId(tid: string | null) {
    currentTenantId.value = tid
  }

  /** 登出：清除Token和状态 */
  function logoutAction() {
    token.value = ''
    user.value = null
    permissionCodes.value = []
    tenantIds.value = []
    currentTenantId.value = null
    clearTokens()
  }

  /** 检查当前用户是否拥有指定权限码 */
  function hasPermission(code: string): boolean {
    return permissionCodes.value.includes(code)
  }

  return {
    token,
    user,
    permissionCodes,
    tenantIds,
    currentTenantId,
    isLoggedIn,
    isPlatformAdmin,
    isSuperAdmin,
    roles,
    displayName,
    loginAction,
    fetchCurrentUser,
    refreshTenantIds,
    setCurrentTenantId,
    logoutAction,
    hasPermission,
  }
})
