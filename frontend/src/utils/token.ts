const ACCESS_TOKEN_KEY = 'accessToken'
const REFRESH_TOKEN_KEY = 'refreshToken'
const EXPIRES_AT_KEY = 'tokenExpiresAt'

/** 存储 accessToken 并推算过期时间戳（保留 60s 安全余量防止时间漂移） */
export function setAccessToken(token: string, expiresIn: number) {
  localStorage.setItem(ACCESS_TOKEN_KEY, token)
  const expiresAt = Date.now() + (expiresIn - 60) * 1000
  localStorage.setItem(EXPIRES_AT_KEY, String(expiresAt))
}

/** 获取 Access Token */
export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_TOKEN_KEY)
}

/** 是否即将过期或已过期 */
export function isTokenExpiring(): boolean {
  const expiresAt = localStorage.getItem(EXPIRES_AT_KEY)
  if (!expiresAt)
    return true
  return Date.now() >= Number(expiresAt)
}

/** 设置 Refresh Token */
export function setRefreshToken(token: string) {
  localStorage.setItem(REFRESH_TOKEN_KEY, token)
}

/** 获取 Refresh Token */
export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY)
}

/** 清除所有本地存储的Token */
export function clearTokens() {
  localStorage.removeItem(ACCESS_TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
  localStorage.removeItem(EXPIRES_AT_KEY)
}
