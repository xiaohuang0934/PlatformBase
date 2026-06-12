import { defineStore } from 'pinia'
import { ref } from 'vue'
import defaultSettings from '@/settings'

export const useSettingsStore = defineStore('settings', () => {
  const fixedHeader = ref(defaultSettings.fixedHeader)
  const sidebarLogo = ref(defaultSettings.sidebarLogo)
  const showTagsView = ref(defaultSettings.showTagsView)
  const showSettings = ref(defaultSettings.showSettings)

  return { fixedHeader, sidebarLogo, showTagsView, showSettings }
})
