import type { LoginRequest, UserProfileDto } from '@/types/auth'
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import * as authApi from '@/api/auth'
import {
  clearTokens,
  setAccessToken,
  setRefreshToken,
} from '@/utils/token'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>(localStorage.getItem('accessToken') || '')
  const user = ref<UserProfileDto | null>(null)
  const permissionCodes = ref<string[]>([])

  const isLoggedIn = computed(() => !!token.value)
  const isSuperAdmin = computed(() => user.value?.userType === 1)
  const roles = computed(() => user.value?.roles ?? [])
  const displayName = computed(() => user.value?.username ?? '')

  /** 登录操作：获取Token并持久化 */
  async function loginAction(data: LoginRequest) {
    const res = await authApi.login(data)
    const { accessToken, refreshToken, expiresIn } = res.data

    setAccessToken(accessToken, expiresIn)
    if (refreshToken) {
      setRefreshToken(refreshToken)
    }

    token.value = accessToken
  }

  /** 获取 Current User */
  async function fetchCurrentUser() {
    const [profileRes, permsRes] = await Promise.all([
      authApi.getProfile(),
      authApi.getPermissions(),
    ])
    user.value = profileRes.data
    permissionCodes.value = permsRes.data
  }

  /** 登出操作：清除Token和状态 */
  function logoutAction() {
    token.value = ''
    user.value = null
    permissionCodes.value = []
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
    isLoggedIn,
    isSuperAdmin,
    roles,
    displayName,
    loginAction,
    fetchCurrentUser,
    logoutAction,
    hasPermission,
  }
})
