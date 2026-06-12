import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/notifications'

export function getNotificationList(params?: { unreadOnly?: boolean, pageIndex?: number, pageSize?: number }): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

export function markAsRead(id: string): Promise<ApiResult<null>> {
  return http.patch(`${BASE}/${id}/read`).then(res => res.data)
}

export function markAllAsRead(): Promise<ApiResult<null>> {
  return http.patch(`${BASE}/read-all`).then(res => res.data)
}
