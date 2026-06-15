import type { AxiosError, InternalAxiosRequestConfig } from 'axios'
import type { ApiResult } from '@/types/api-result'
import axios from 'axios'
import { redirectToLogin, refreshAccessToken } from '@/utils/refresh'
import { getAccessToken, isTokenExpiring } from '@/utils/token'

const http = axios.create({
  baseURL: '/api/v1',
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
  paramsSerializer: (params) => {
    const parts: string[] = []
    for (const [key, val] of Object.entries(params)) {
      if (val == null || val === '')
        continue
      if (Array.isArray(val))
        val.forEach(v => parts.push(`${encodeURIComponent(key)}=${encodeURIComponent(v)}`))
      else
        parts.push(`${encodeURIComponent(key)}=${encodeURIComponent(val)}`)
    }
    return parts.join('&')
  },
})

/** 请求拦截：自动刷新即将过期的 token，并附加 Bearer 头 */
http.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    const token = getAccessToken()

    // 无 token → 匿名请求（如登录），直接放行
    if (!token) {
      return config
    }

    // 刷新请求本身不触发二次刷新
    if (config.url?.includes('/auth/refresh')) {
      config.headers.Authorization = `Bearer ${token}`
      return config
    }

    // token 即将过期 → 先静默刷新再发请求
    if (isTokenExpiring()) {
      try {
        const newToken = await refreshAccessToken()
        config.headers.Authorization = `Bearer ${newToken}`
      }
      catch {
        redirectToLogin()
        return Promise.reject(new Error('登录已过期'))
      }
    }
    else {
      config.headers.Authorization = `Bearer ${token}`
    }

    return config
  },
  (error: AxiosError) => Promise.reject(error),
)

/** 响应拦截：统一解包 + 401 自动刷新重试 + 业务错误提示 */
http.interceptors.response.use(
  (response) => {
    const body = response.data as ApiResult

    if (body.success) {
      return response
    }

    import('element-plus').then(({ ElMessage }) => {
      ElMessage.error(body.message || '请求失败')
    })
    return Promise.reject(new Error(body.message || '请求失败'))
  },
  async (error: AxiosError<ApiResult>) => {
    const status = error.response?.status
    const config = error.config as InternalAxiosRequestConfig & { _retry?: boolean }

    // 401 → 尝试刷新后重试一次（登录接口、刷新接口本身除外）
    if (status === 401 && !config._retry && !config.url?.includes('/auth/login') && !config.url?.includes('/auth/refresh')) {
      config._retry = true

      try {
        const newToken = await refreshAccessToken()
        config.headers.Authorization = `Bearer ${newToken}`
        return http(config)
      }
      catch {
        redirectToLogin()
        return Promise.reject(new Error('登录已过期，请重新登录'))
      }
    }

    // 刷新接口本身 401 → 无法恢复
    if (status === 401 && config.url?.includes('/auth/refresh')) {
      redirectToLogin()
      return Promise.reject(new Error('登录已过期'))
    }

    if (status === 403) {
      import('element-plus').then(({ ElMessage }) => {
        ElMessage.error('无权限访问')
      })
      return Promise.reject(new Error('无权限访问'))
    }

    const msg = error.response?.data?.message || error.message || '网络异常'
    import('element-plus').then(({ ElMessage }) => {
      ElMessage.error(msg)
    })
    return Promise.reject(error)
  },
)

export default http
