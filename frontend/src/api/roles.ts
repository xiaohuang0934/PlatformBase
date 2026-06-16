import type { ApiResult, PagedResult } from '@/types/api-result'
import type { CreateRoleDto, RoleDto, RoleQuery } from '@/types/auth'
import http from './index'

const BASE = '/roles'

/** 分页查询角色列表 */
export function getRoleList(params?: RoleQuery): Promise<ApiResult<PagedResult<RoleDto>>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取角色详情 */
export function getRoleById(id: string): Promise<ApiResult<RoleDto>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 创建角色 */
export function createRole(data: CreateRoleDto): Promise<ApiResult<RoleDto>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新角色信息 */
export function updateRole(id: string, data: { name?: string, description?: string }): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除角色 */
export function deleteRole(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}

/** 获取角色权限编码列表 */
export function getRolePermissions(id: string): Promise<ApiResult<string[]>> {
  return http.get(`${BASE}/${id}/permissions`).then(res => res.data)
}

/** 全量替换角色权限（body 为权限编码数组） */
export function assignRolePermissions(id: string, permissionCodes: string[]): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}/permissions`, permissionCodes).then(res => res.data)
}
