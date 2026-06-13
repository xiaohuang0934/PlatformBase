import type { ApiResult } from '@/types/api-result'
import type { LoginResponse } from '@/types/auth'
import axios from 'axios'
import {
  clearTokens,
  getRefreshToken,
  setAccessToken,
  setRefreshToken,
} from './token'

/** 用于刷新请求的独立 axios 实例（无拦截器，避免循环依赖） */
const refreshHttp = axios.create({
  baseURL: '/api/v1',
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
})

let isRefreshing = false
let pendingRequests: Array<{
  resolve: (token: string) => void
  reject: (err: unknown) => void
}> = []

/** 静默刷新 token，并发请求自动排队等待 */
export async function refreshAccessToken(): Promise<string> {
  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    clearTokens()
    throw new Error('无 RefreshToken，请重新登录')
  }

  if (isRefreshing) {
    return new Promise<string>((resolve, reject) => {
      pendingRequests.push({ resolve, reject })
    })
  }

  isRefreshing = true

  try {
    const res = await refreshHttp.post<ApiResult<LoginResponse>>('/auth/refresh', { refreshToken })
    const { accessToken, refreshToken: newRefreshToken, expiresIn } = res.data.data

    setAccessToken(accessToken, expiresIn)
    if (newRefreshToken) {
      setRefreshToken(newRefreshToken)
    }

    for (const req of pendingRequests) {
      req.resolve(accessToken)
    }

    return accessToken
  }
  catch (err) {
    clearTokens()
    for (const req of pendingRequests) {
      req.reject(err)
    }
    throw err
  }
  finally {
    isRefreshing = false
    pendingRequests = []
  }
}

export function redirectToLogin() {
  clearTokens()
  window.location.href = '/login'
}
