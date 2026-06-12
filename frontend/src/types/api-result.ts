/** 统一 API 响应格式 */
export interface ApiResult<T = unknown> {
  success: boolean
  code: number
  message: string
  data: T
  traceId: string | null
}

/** 分页结果 */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageIndex: number
  pageSize: number
}

/** 分页请求参数 */
export interface PagedRequest {
  pageIndex?: number
  pageSize?: number
  keyword?: string
  sortField?: string
  sortOrder?: 'asc' | 'desc'
}
