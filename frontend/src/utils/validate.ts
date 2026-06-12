/** 表单校验工具 */

/** URL 是否合法 */
export function isExternal(path: string): boolean {
  return /^(?:https?:|mailto:|tel:)/.test(path)
}

/** 合法手机号 */
export function isValidPhone(phone: string): boolean {
  return /^1[3-9]\d{9}$/.test(phone)
}

/** 合法邮箱 */
export function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@][^\s.@]*\.[^\s@]+$/.test(email)
}

/** 合法用户名 (3-20 位字母数字下划线) */
export function isValidUsername(name: string): boolean {
  return /^\w{3,20}$/.test(name)
}

/** 合法密码 (6-30 位) */
export function isValidPassword(pwd: string): boolean {
  return pwd.length >= 6 && pwd.length <= 30
}
