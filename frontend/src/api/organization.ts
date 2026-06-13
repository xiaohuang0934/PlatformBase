import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/organization-units'

/** 获取 Org Unit Tree */
export function getOrgUnitTree(): Promise<ApiResult<any[]>> {
  return http.get(BASE).then(res => res.data)
}

/** 获取 Org Unit By Id */
export function getOrgUnitById(id: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 创建组织节点 */
export function createOrgUnit(data: { name: string, parentId?: string, description?: string }): Promise<ApiResult<any>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新组织信息 */
export function updateOrgUnit(id: string, data: Record<string, unknown>): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除ete Org Unit */
export function deleteOrgUnit(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}
