import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/operation-logs'

export function getLogList(params?: Record<string, unknown>): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

export function getLogById(id: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

export function cleanupLogs(daysAgo = 90): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/cleanup`, { params: { daysAgo } }).then(res => res.data)
}
