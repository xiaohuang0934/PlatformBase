import type { ApiResult, PagedResult } from '@/types/api-result'
import type { OrgUnitNode } from '@/types/user'
import type { AssignRolesDto, CreateUserDto, UpdateUserDto, UserDto, UserQuery } from '@/types/user'
import http from './index'

const BASE = '/users'

/** 分页查询用户列表 */
export function getUserList(params?: UserQuery): Promise<ApiResult<PagedResult<UserDto>>> {
  return http.get(BASE, {
    params: params?.tenantIds?.length
      ? { ...params, tenantIds: params.tenantIds }
      : params,
  }).then(res => res.data)
}

/** 获取用户详情 */
export function getUserById(id: string): Promise<ApiResult<UserDto>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 创建用户 */
export function createUser(data: CreateUserDto): Promise<ApiResult<UserDto>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新用户信息 */
export function updateUser(id: string, data: UpdateUserDto): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除用户 */
export function deleteUser(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}

/** 切换用户启用/禁用状态 */
export function toggleUser(id: string): Promise<ApiResult<null>> {
  return http.patch(`${BASE}/${id}/toggle`).then(res => res.data)
}

/** 重置密码 */
export function resetPassword(id: string, newPassword: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${id}/reset-password`, { newPassword }).then(res => res.data)
}

/** 获取用户角色 */
export function getUserRoles(id: string): Promise<ApiResult<string[]>> {
  return http.get(`${BASE}/${id}/roles`).then(res => res.data)
}

/** 全量替换用户角色 */
export function assignUserRoles(id: string, data: AssignRolesDto): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}/roles`, data).then(res => res.data)
}

/** 获取用户所属部门 */
export function getUserOrganizations(id: string): Promise<ApiResult<OrgUnitNode[]>> {
  return http.get(`${BASE}/${id}/organizations`).then(res => res.data)
}

/** 全量替换用户部门 */
export function assignOrganizations(id: string, organizationUnitIds: string[]): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}/organizations`, organizationUnitIds).then(res => res.data)
}

/** 添加用户到部门 */
export function addToOrganization(userId: string, orgId: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${userId}/organizations/${orgId}`).then(res => res.data)
}

/** 将用户移出部门 */
export function removeFromOrganization(userId: string, orgId: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${userId}/organizations/${orgId}`).then(res => res.data)
}
