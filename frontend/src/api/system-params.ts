import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/system-params'

/** 获取 Param List */
export function getParamList(params?: Record<string, unknown>): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取 Param By Code */
export function getParamByCode(code: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${code}`).then(res => res.data)
}

/** 创建参数 */
export function createParam(data: { code: string, value: string, category?: string, description?: string }): Promise<ApiResult<any>> {
  return http.post(BASE, data).then(res => res.data)
}

/** 更新参数 */
export function updateParam(id: string, data: Record<string, unknown>): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

/** 删除ete Param */
export function deleteParam(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}
