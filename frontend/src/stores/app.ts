import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useAppStore = defineStore('app', () => {
  const sidebarCollapsed = ref(false)
  const isMobile = ref(false)

  /** 切换侧边栏折叠 */
  function toggleSidebar() {
    sidebarCollapsed.value = !sidebarCollapsed.value
  }

  /** 设置 Device */
  function setDevice(mobile: boolean) {
    isMobile.value = mobile
  }

  return { sidebarCollapsed, isMobile, toggleSidebar, setDevice }
})
