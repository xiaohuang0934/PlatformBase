/** 权限判断工具函数 */

export function checkPermission(has: (code: string) => boolean, codes: string[], mode: 'some' | 'every' = 'every'): boolean {
  if (!codes.length)
    return true
  return mode === 'some'
    ? codes.some(code => has(code))
    : codes.every(code => has(code))
}
