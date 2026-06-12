import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/organization-units'

export function getOrgUnitTree(): Promise<ApiResult<any[]>> {
  return http.get(BASE).then(res => res.data)
}

export function getOrgUnitById(id: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

export function createOrgUnit(data: { name: string, parentId?: string, description?: string }): Promise<ApiResult<any>> {
  return http.post(BASE, data).then(res => res.data)
}

export function updateOrgUnit(id: string, data: Record<string, unknown>): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${id}`, data).then(res => res.data)
}

export function deleteOrgUnit(id: string): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/${id}`).then(res => res.data)
}
