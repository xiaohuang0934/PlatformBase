/** 通用工具函数 */

export function debounce<T extends (...args: any[]) => any>(fn: T, delay = 300): (...args: Parameters<T>) => void {
  let timer: ReturnType<typeof setTimeout>
  return (...args: Parameters<T>) => {
    clearTimeout(timer)
    timer = setTimeout(() => fn(...args), delay)
  }
}

export function throttle<T extends (...args: any[]) => any>(fn: T, delay = 300): (...args: Parameters<T>) => void {
  let last = 0
  return (...args: Parameters<T>) => {
    const now = Date.now()
    if (now - last >= delay) {
      last = now
      fn(...args)
    }
  }
}

export function deepClone<T>(obj: T): T {
  return JSON.parse(JSON.stringify(obj))
}

export function parseTime(time: string, format = '{y}-{m}-{d} {h}:{i}:{s}'): string {
  const date = new Date(time)
  const map: Record<string, number> = {
    y: date.getFullYear(),
    m: date.getMonth() + 1,
    d: date.getDate(),
    h: date.getHours(),
    i: date.getMinutes(),
    s: date.getSeconds(),
  }
  return format.replace(/\{([ymdhis])\}/g, (_, key) => {
    const val = map[key]
    return val < 10 ? `0${val}` : `${val}`
  })
}

/** 参数对象转 URL query string */
export function param2Obj(url: string): Record<string, string> {
  const search = url.split('?')[1]
  if (!search)
    return {}
  return JSON.parse(
    `{"${decodeURIComponent(search).replace(/"/g, '\\"').replace(/&/g, '","').replace(/=/g, '":"')}"}`,
  )
}
