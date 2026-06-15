import type { PagedRequest } from './api-result'

/** 部门树节点（匹配后端 OrgUnitNode） */
export interface OrgUnitNode {
  id: string
  name: string
  code: string
  parentId: string | null
  sortOrder: number
}

/** 用户类型枚举 */
export enum UserType {
  PlatformAdmin = 1,
  TenantAdmin = 2,
  TenantUser = 3,
}

/** 用户列表项（匹配后端 UserDto） */
export interface UserDto {
  id: string
  username: string
  email: string | null
  emailConfirmed: boolean
  phoneNumber: string | null
  isActive: boolean
  userType: number
  roles: string[]
  organizationUnits: OrgUnitNode[]
  tenantName: string | null
  createdAt: string
  updatedAt: string | null
}

/** 创建用户 */
export interface CreateUserDto {
  username: string
  password: string
  email?: string
  phoneNumber?: string
  tenantId?: string
  userType?: number
  roleIds: string[]
  organizationUnitIds: string[]
}

/** 更新用户 */
export interface UpdateUserDto {
  email?: string
  phoneNumber?: string
  isActive?: boolean
  userType?: number
  roleIds?: string[]
  organizationUnitIds?: string[]
}

/** 分配角色 */
export interface AssignRolesDto {
  roleIds: string[]
}

/** 用户查询参数 */
export interface UserQuery extends PagedRequest {
  isActive?: boolean
  tenantIds?: string[]
}
