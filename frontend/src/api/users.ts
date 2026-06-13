import type { ApiResult, PagedResult } from '@/types/api-result'
import type { AssignRolesDto, CreateUserDto, UpdateUserDto, UserDto, UserQuery } from '@/types/user'
import http from './index'

const BASE = '/users'

/** 获取 User List */
export function getUserList(params?: UserQuery): Promise<ApiResult<PagedResult<UserDto>>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取 User By Id */
export function getUserById(id: string): Promise<ApiResult<UserDto>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 创建用户（含角色分配） */
export function createUser(data: CreateUserDto): Promise<ApiResult<UserDto>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新用户信息 */
export function updateUser(id: string, data: UpdateUserDto): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除ete User */
export function deleteUser(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}

/** 切换用户启用/禁用状态 */
export function toggleUser(id: string): Promise<ApiResult<null>> {
  return http.patch(`${BASE}/${id}/toggle`).then(res => res.data)
}

/** Re设置 Password */
export function resetPassword(id: string, newPassword: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${id}/reset-password`, { newPassword }).then(res => res.data)
}

/** 获取 User Roles */
export function getUserRoles(id: string): Promise<ApiResult<string[]>> {
  return http.get(`${BASE}/${id}/roles`).then(res => res.data)
}

/** 全量替换用户角色 */
export function assignUserRoles(id: string, data: AssignRolesDto): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}/roles`, data).then(res => res.data)
}
