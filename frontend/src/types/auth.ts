import type { PagedRequest } from './api-result'

/** 登录请求 */
export interface LoginRequest {
  username: string
  password: string
}

/** 登录响应（匹配后端 LoginResponse） */
export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresIn: number
  refreshToken: string | null
}

/** 用户资料（匹配后端 UserProfileDto） */
export interface UserProfileDto {
  id: string
  username: string
  email: string | null
  emailConfirmed: boolean
  phoneNumber: string | null
  isActive: boolean
  userType: number
  roles: string[]
  createdAt: string
  updatedAt: string | null
}

/** 角色（匹配后端 RoleDto） */
export interface RoleDto {
  id: string
  name: string
  code: string
  description: string | null
  isSystem: boolean
  createdAt: string
}

export interface RoleQuery extends PagedRequest {
  isSystem?: boolean
}

/** 权限码 */
export interface PermissionDto {
  id: string
  name: string
  code: string
  group: string | null
  description: string | null
}

/** 菜单节点（匹配后端 MenuDto） */
export interface MenuDto {
  id: string
  name: string
  path: string
  icon: string | null
  parentId: string | null
  permissionCode: string | null
  sort: number
  isVisible: boolean
  type?: number
  keepAlive?: boolean
  children: MenuDto[]
}
