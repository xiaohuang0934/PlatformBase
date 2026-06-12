import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/tenants'

export function getTenantList(params?: Record<string, unknown>): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

export function getTenantById(id: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

export function createTenant(data: { name: string, code: string, description?: string }): Promise<ApiResult<any>> {
  return http.post(BASE, data).then(res => res.data)
}

export function updateTenant(id: string, data: Record<string, unknown>): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

export function deleteTenant(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}
