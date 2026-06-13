import http from '@/api/index'

/** 通过 axios 下载文件（自动携带 JWT token） */
export async function downloadFile(url: string, filename?: string) {
  try {
    const res = await http.get(url, { responseType: 'blob' })
    const blob = new Blob([res.data])
    const downloadUrl = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = downloadUrl
    // 尝试从 Content-Disposition 头解析文件名
    const disposition = res.headers['content-disposition'] as string
    if (disposition) {
      const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/)
      if (match)
        link.download = match[1].replace(/['"]/g, '')
    }
    if (!link.download && filename)
      link.download = filename
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(downloadUrl)
  }
  catch {
    // 错误由响应拦截器统一处理
  }
}
