import type { ApiResult, PagedResult } from '@/types/api-result'
import type { UserDto, UserQuery } from '@/types/user'
import http from './index'

const BASE = '/organization-units'

/** 获取部门全量树（用于 OrgSelector 组件） */
export function getOrgUnitTree(): Promise<ApiResult<any[]>> {
  return http.get(BASE, { params: { mode: 'tree' } }).then(res => res.data)
}

/** 懒加载：租户摘要（无参）或指定租户/父级的部门列表 */
export function getOrgNodes(params?: { tenantId?: string, parentId?: string }): Promise<ApiResult<any[]>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取部门详情 */
export function getOrgUnitById(id: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 创建部门 */
export function createOrgUnit(data: { name: string, code: string, tenantId?: string, parentId?: string, description?: string }): Promise<ApiResult<any>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新部门 */
export function updateOrgUnit(id: string, data: Record<string, unknown>): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除部门 */
export function deleteOrgUnit(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}

/** 查询部门下的用户（仅本部门） */
export function getOrgUsers(orgId: string): Promise<ApiResult<UserDto[]>> {
  return http.get(`${BASE}/${orgId}/users`).then(res => res.data)
}

/** 分页查询部门及子级用户 */
export function getOrgUsersPaged(orgId: string, query?: UserQuery): Promise<ApiResult<PagedResult<UserDto>>> {
  return http.get(`${BASE}/${orgId}/users/with-children`, { params: query }).then(res => res.data)
}
