import type { PagedRequest } from './api-result'

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
  createdAt: string
  updatedAt: string | null
}

/** 创建用户 */
export interface CreateUserDto {
  username: string
  password: string
  email?: string
  phoneNumber?: string
  roleIds?: string[]
}

/** 更新用户 */
export interface UpdateUserDto {
  email?: string
  phoneNumber?: string
  isActive?: boolean
  roleIds?: string[]
}

/** 分配角色 */
export interface AssignRolesDto {
  roleIds: string[]
}

/** 用户查询参数 */
export interface UserQuery extends PagedRequest {
  isActive?: boolean
}
