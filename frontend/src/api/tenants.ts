import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/tenants'

export interface TenantDto {
  id: string
  name: string
  code: string
  description?: string
  isEnabled: boolean
  createdAt: string
}

/** 分页查询租户列表 */
export function getTenantList(params?: { keyword?: string, isEnabled?: boolean, pageIndex?: number, pageSize?: number }): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取租户详情 */
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

/** 删除租户 */
export function deleteTenant(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}

/** 获取当前平台用户可访问的租户列表 */
export function getAccessibleTenants(): Promise<ApiResult<TenantDto[]>> {
  return http.get(`${BASE}/accessible`).then(res => res.data)
}
