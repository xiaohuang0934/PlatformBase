import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/tenant-params'

/** 分页查询租户参数列表 */
export function getPaged(params?: {
  tenantId?: string
  keyword?: string
  pageIndex?: number
  pageSize?: number
}): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取单个租户参数覆盖值 */
export function getByCode(code: string, tenantId?: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${code}`, { params: { tenantId } }).then(res => res.data)
}

/** 创建租户参数覆盖 */
export function create(data: {
  code: string
  value: string
  category?: string
  description?: string
}): Promise<ApiResult<any>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新租户参数覆盖 */
export function update(id: string, data: { value?: string, description?: string }): Promise<ApiResult<any>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除租户参数覆盖 */
export function remove(id: string): Promise<ApiResult<any>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}

/** 获取全部租户列表（平台管理员选择租户用） */
export function getAllTenants(): Promise<ApiResult<any>> {
  return http.get('/tenants', { params: { pageIndex: 1, pageSize: 100 } }).then(res => res.data)
}
