import type { ApiResult } from '@/types/api-result'
import type { LoginRequest, LoginResponse, UserProfileDto } from '@/types/auth'
import http from './index'

const BASE = '/auth'

/** 用户登录，返回 AccessToken + RefreshToken */
export function login(data: LoginRequest): Promise<ApiResult<LoginResponse>> {
  return http.post(`${BASE}/login`, data).then(res => res.data)
}

/** 获取当前登录用户的资料信息 */
export function getProfile(): Promise<ApiResult<UserProfileDto>> {
  return http.get(`${BASE}/profile`).then(res => res.data)
}

/** 获取当前用户的所有权限编码列表 */
export function getPermissions(): Promise<ApiResult<string[]>> {
  return http.get(`${BASE}/permissions`).then(res => res.data)
}

/** 刷新 Token：用 RefreshToken 换新的 AccessToken */
export function refreshToken(refreshToken: string): Promise<ApiResult<LoginResponse>> {
  return http.post(`${BASE}/refresh`, { refreshToken }).then(res => res.data)
}

/** 修改当前用户密码 */
export function changePassword(data: { currentPassword: string, newPassword: string }): Promise<ApiResult<null>> {
  return http.post(`${BASE}/change-password`, data).then(res => res.data)
}
