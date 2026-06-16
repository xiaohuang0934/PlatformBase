import type { ApiResult } from '@/types/api-result'
import type { MenuDto } from '@/types/auth'
import http from './index'

/** 获取当前用户可访问的菜单树（已按权限裁剪） */
export function getMenuTree(): Promise<ApiResult<MenuDto[]>> {
  return http.get('/menus/tree').then(res => res.data)
}

/** 获取菜单列表（支持按父级ID筛选：不传返回一级菜单，传值返回该父级下的子菜单） */
export function getMenuList(parentId?: string): Promise<ApiResult<MenuDto[]>> {
  return http.get('/menus', { params: parentId ? { parentId } : {} }).then(res => res.data)
}

/** 获取 Menu By Id */
export function getMenuById(id: string): Promise<ApiResult<MenuDto>> {
  return http.get(`/menus/${id}`).then(res => res.data)
}

/** 创建菜单 */
export function createMenu(data: {
  name: string
  type: number
  parentId?: string
  path?: string
  icon?: string
  permissionCode?: string
  sortOrder?: number
  isVisible?: boolean
}): Promise<ApiResult<MenuDto>> {
  return http.post('/menus', data).then(res => res.data)
}

/** 更新菜单信息 */
export function updateMenu(id: string, data: {
  name?: string
  icon?: string
  path?: string
  permissionCode?: string
  sortOrder?: number
  isVisible?: boolean
}): Promise<ApiResult<null>> {
  return http.put(`/menus/${id}`, data).then(res => res.data)
}

/** 删除ete Menu */
export function deleteMenu(id: string): Promise<ApiResult<null>> {
  return http.delete(`/menus/${id}`).then(res => res.data)
}

/** 为用户分配菜单（全量替换） */
export function assignMenus(userId: string, menuIds: string[]): Promise<ApiResult<any>> {
  return http.put(`/users/${userId}/menus`, menuIds).then(res => res.data)
}

/** 获取用户已分配的菜单 ID 列表 */
export function getUserMenus(userId: string): Promise<ApiResult<string[]>> {
  return http.get(`/users/${userId}/menus`).then(res => res.data)
}
