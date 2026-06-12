import type { ApiResult, PagedRequest, PagedResult } from '@/types/api-result'
import type { PermissionDto } from '@/types/auth'
import http from './index'

const BASE = '/permissions'

export function getPermissionList(params?: PagedRequest): Promise<ApiResult<PagedResult<PermissionDto>>> {
  return http.get(BASE, { params }).then(res => res.data)
}

export function getPermissionById(id: string): Promise<ApiResult<PermissionDto>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

export function createPermission(data: { name: string, code: string, group?: string, description?: string }): Promise<ApiResult<PermissionDto>> {
  return http.post(BASE, data).then(res => res.data)
}

export function updatePermission(id: string, data: { name?: string, code?: string, group?: string, description?: string }): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

export function deletePermission(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}
