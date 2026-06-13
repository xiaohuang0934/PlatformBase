import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/notifications'

/** 获取 Notification List */
export function getNotificationList(params?: { unreadOnly?: boolean, pageIndex?: number, pageSize?: number }): Promise<ApiResult<any>> {
  return http.get(BASE, { params }).then(res => res.data)
}

/** 标记 As Read */
export function markAsRead(id: string): Promise<ApiResult<null>> {
  return http.patch(`${BASE}/${id}/read`).then(res => res.data)
}

/** 标记 All As Read */
export function markAllAsRead(): Promise<ApiResult<null>> {
  return http.patch(`${BASE}/read-all`).then(res => res.data)
}
