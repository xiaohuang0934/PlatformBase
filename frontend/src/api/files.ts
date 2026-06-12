import type { ApiResult } from '@/types/api-result'
import http from './index'

export function getFileList(params: { bizType: string, bizId: string }): Promise<ApiResult<any[]>> {
  return http.get('/files', { params }).then(res => res.data)
}

export function deleteFile(id: string): Promise<ApiResult<null>> {
  return http.delete(`/files/${id}`).then(res => res.data)
}

export function getDownloadUrl(id: string): string {
  return `/api/v1/files/${id}/download`
}
