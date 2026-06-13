import type { ApiResult } from '@/types/api-result'
import http from './index'

/** 获取 Dict Types */
export function getDictTypes(params?: Record<string, unknown>): Promise<ApiResult<any>> {
  return http.get('/data-dict/types', { params }).then(res => res.data)
}

/** 创建字典类型 */
export function createDictType(data: { typeName: string, typeCode: string, description?: string, sortOrder?: number }): Promise<ApiResult<any>> {
  return http.post('/data-dict/types', data).then(res => res.data)
}

/** 更新字典类型 */
export function updateDictType(id: string, data: Record<string, unknown>): Promise<ApiResult<null>> {
  return http.put(`/data-dict/types/${id}`, data).then(res => res.data)
}

/** 删除ete Dict Type */
export function deleteDictType(id: string): Promise<ApiResult<null>> {
  return http.delete(`/data-dict/types/${id}`).then(res => res.data)
}

/** 获取 Dict Items */
export function getDictItems(dictTypeId: string): Promise<ApiResult<any[]>> {
  return http.get(`/data-dict/types/${dictTypeId}/items`).then(res => res.data)
}

/** 创建字典项 */
export function createDictItem(data: { dictTypeId: string, itemName: string, itemCode: string, itemValue?: string, sortOrder?: number, parentId?: string }): Promise<ApiResult<any>> {
  return http.post('/data-dict/items', data).then(res => res.data)
}

/** 更新字典项 */
export function updateDictItem(id: string, data: Record<string, unknown>): Promise<ApiResult<null>> {
  return http.put(`/data-dict/items/${id}`, data).then(res => res.data)
}

/** 删除ete Dict Item */
export function deleteDictItem(id: string): Promise<ApiResult<null>> {
  return http.delete(`/data-dict/items/${id}`).then(res => res.data)
}
