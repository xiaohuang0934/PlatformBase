import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/jobs'

/** 获取 Job List */
export function getJobList(): Promise<ApiResult<any[]>> {
  return http.get(BASE).then(res => res.data)
}

/** 获取 Job By Id */
export function getJobById(jobId: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${jobId}`).then(res => res.data)
}

/** 启动定时任务 */
export function startJob(jobId: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${jobId}/start`).then(res => res.data)
}

/** 停止定时任务 */
export function stopJob(jobId: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${jobId}/stop`).then(res => res.data)
}

/** 手动触发执行 */
export function triggerJob(jobId: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${jobId}/trigger`).then(res => res.data)
}

/** 修改任务Cron表达式 */
export function updateJobCron(jobId: string, cron: string): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${jobId}/cron`, { cron }).then(res => res.data)
}
