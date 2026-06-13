import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/operation-logs'

/** 获取 Log List */
export function getLogList(params?: Record<string, unknown>): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 获取 Log By Id */
export function getLogById(id: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${id}`).then(res => res.data)
}

/** 清理N天前的旧日志 */
export function cleanupLogs(daysAgo = 90): Promise<ApiResult<null>> {
  return http.delete(`${BASE}/cleanup`, { params: { daysAgo } }).then(res => res.data)
}
