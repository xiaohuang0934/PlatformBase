import type { ApiResult, PagedResult } from '@/types/api-result'
import type { RoleDto, RoleQuery } from '@/types/auth'
import http from './index'

const BASE = '/roles'

/** 获取 Role List */
export function getRoleList(params?: RoleQuery): Promise<ApiResult<PagedResult<RoleDto>>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取 Role By Id */
export function getRoleById(id: string): Promise<ApiResult<RoleDto>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 创建角色 */
export function createRole(data: { name: string, code: string, description?: string }): Promise<ApiResult<RoleDto>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新角色信息 */
export function updateRole(id: string, data: { name?: string, description?: string }): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除ete Role */
export function deleteRole(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}

/** 获取 Role Permissions */
export function getRolePermissions(id: string): Promise<ApiResult<string[]>> {
  return http.get(`${BASE}/${id}/permissions`).then(res => res.data)
}

/** 全量替换角色权限 */
export function assignRolePermissions(id: string, data: { permissionIds: string[] }): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}/permissions`, data).then(res => res.data)
}
