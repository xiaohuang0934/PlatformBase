import type { MenuDto } from '@/types/auth'
import { defineStore } from 'pinia'
import { ref } from 'vue'
import * as menuApi from '@/api/menus'

export const usePermissionStore = defineStore('permission', () => {
  const menuTree = ref<MenuDto[]>([])
  const menuLoaded = ref(false)
  const routesAdded = ref(false)

  async function fetchMenus() {
    const res = await menuApi.getMenuTree()
    menuTree.value = res.data
    menuLoaded.value = true
  }

  function markRoutesAdded() {
    routesAdded.value = true
  }

  function flattenMenus(menus: MenuDto[]): MenuDto[] {
    const result: MenuDto[] = []
    function walk(list: MenuDto[]) {
      for (const item of list) {
        result.push(item)
        if (item.children?.length)
          walk(item.children)
      }
    }
    walk(menus)
    return result
  }

  function reset() {
    menuTree.value = []
    menuLoaded.value = false
    routesAdded.value = false
  }

  return { menuTree, menuLoaded, routesAdded, fetchMenus, markRoutesAdded, flattenMenus, reset }
})
