/** 动态设置页面标题 */
import defaultSettings from '@/settings'

const title = defaultSettings.title || 'PlatformBase'

export function getPageTitle(pageTitle: string): string {
  if (pageTitle) {
    return `${pageTitle} - ${title}`
  }
  return title
}
