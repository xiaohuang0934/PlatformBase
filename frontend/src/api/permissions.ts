import type { ApiResult, PagedRequest, PagedResult } from '@/types/api-result'
import type { PermissionDto } from '@/types/auth'
import http from './index'

const BASE = '/permissions'

/** 获取 Permission List */
export function getPermissionList(params?: PagedRequest): Promise<ApiResult<PagedResult<PermissionDto>>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取 Permission By Id */
export function getPermissionById(id: string): Promise<ApiResult<PermissionDto>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 创建权限 */
export function createPermission(data: { name: string, code: string, resourcePath: string, httpMethod: string, group?: string, description?: string }): Promise<ApiResult<PermissionDto>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新权限信息 */
export function updatePermission(id: string, data: { name?: string, group?: string, description?: string }): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除ete Permission */
export function deletePermission(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}
