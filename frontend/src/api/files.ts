import type { ApiResult } from '@/types/api-result'
import http from './index'

/** 获取 File List */
export function getFileList(params: { bizType: string, bizId: string }): Promise<ApiResult<any[]>> {
  return http.get('/files', { params }).then(res => res.data)
}

/** 删除ete File */
export function deleteFile(id: string): Promise<ApiResult<null>> {
  return http.delete(`/files/${id}`).then(res => res.data)
}
