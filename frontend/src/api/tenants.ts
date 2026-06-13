import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/tenants'

/** 获取 Tenant List */
export function getTenantList(params?: { keyword?: string, isEnabled?: boolean, pageIndex?: number, pageSize?: number }): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取 Tenant By Id */
export function getTenantById(id: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 创建租户 */
export function createTenant(data: { name: string, code: string, description?: string }): Promise<ApiResult<any>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新租户信息 */
export function updateTenant(id: string, data: Record<string, unknown>): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除ete Tenant */
export function deleteTenant(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}
