const BASE = '/import-export'

/** 导出用户 Excel（blob 下载由 downloadFile 工具处理） */
export function getExportUsersUrl(): string {
  return `${BASE}/users`
}
