import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export type ThemeMode = 'dark' | 'light'

const STORAGE_KEY = 'platformbase-theme'

/** 获取 Initial Theme */
function getInitialTheme(): ThemeMode {
  const stored = localStorage.getItem(STORAGE_KEY)
  if (stored === 'light' || stored === 'dark')
    return stored
  return 'dark'
}

/** 应用主题到documentElement */
function applyTheme(mode: ThemeMode) {
  document.documentElement.setAttribute('data-theme', mode)
}

export const useThemeStore = defineStore('theme', () => {
  const mode = ref<ThemeMode>(getInitialTheme())

  applyTheme(mode.value)

  watch(mode, (val) => {
    localStorage.setItem(STORAGE_KEY, val)
    applyTheme(val)
  })

  /** 切换暗/亮模式 */
  function toggle() {
    mode.value = mode.value === 'dark' ? 'light' : 'dark'
  }

  return { mode, toggle }
})
