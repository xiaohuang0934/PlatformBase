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

  async function loginAction(data: LoginRequest) {
    const res = await authApi.login(data)
    const { accessToken, refreshToken, expiresIn } = res.data

    setAccessToken(accessToken, expiresIn)
    if (refreshToken) {
      setRefreshToken(refreshToken)
    }

    token.value = accessToken
  }

  async function fetchCurrentUser() {
    const [profileRes, permsRes] = await Promise.all([
      authApi.getProfile(),
      authApi.getPermissions(),
    ])
    user.value = profileRes.data
    permissionCodes.value = permsRes.data
  }

  function logoutAction() {
    token.value = ''
    user.value = null
    permissionCodes.value = []
    clearTokens()
  }

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
