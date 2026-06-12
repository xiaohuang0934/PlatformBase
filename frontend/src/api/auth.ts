import type { ApiResult } from '@/types/api-result'
import type { LoginRequest, LoginResponse, UserProfileDto } from '@/types/auth'
import http from './index'

const BASE = '/auth'

export function login(data: LoginRequest): Promise<ApiResult<LoginResponse>> {
  return http.post(`${BASE}/login`, data).then(res => res.data)
}

export function getProfile(): Promise<ApiResult<UserProfileDto>> {
  return http.get(`${BASE}/profile`).then(res => res.data)
}

export function getPermissions(): Promise<ApiResult<string[]>> {
  return http.get(`${BASE}/permissions`).then(res => res.data)
}

export function refreshToken(refreshToken: string): Promise<ApiResult<LoginResponse>> {
  return http.post(`${BASE}/refresh`, { refreshToken }).then(res => res.data)
}

export function changePassword(data: { currentPassword: string, newPassword: string }): Promise<ApiResult<null>> {
  return http.post(`${BASE}/change-password`, data).then(res => res.data)
}
