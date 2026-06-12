import type { ApiResult } from '@/types/api-result'
import http from './index'

const BASE = '/jobs'

export function getJobList(): Promise<ApiResult<any[]>> {
  return http.get(BASE).then(res => res.data)
}

export function getJobById(jobId: string): Promise<ApiResult<any>> {
  return http.get(`${BASE}/${jobId}`).then(res => res.data)
}

export function startJob(jobId: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${jobId}/start`).then(res => res.data)
}

export function stopJob(jobId: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${jobId}/stop`).then(res => res.data)
}

export function triggerJob(jobId: string): Promise<ApiResult<null>> {
  return http.post(`${BASE}/${jobId}/trigger`).then(res => res.data)
}

export function updateJobCron(jobId: string, cron: string): Promise<ApiResult<null>> {
  return http.put(`${BASE}/${jobId}/cron`, { cron }).then(res => res.data)
}
