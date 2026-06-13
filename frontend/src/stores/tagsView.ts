import type { RouteLocationNormalized } from 'vue-router'
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

export interface TagView extends Partial<RouteLocationNormalized> {
  title?: string
  path?: string
  name?: string
  fullPath?: string
  query?: Record<string, any>
  meta?: any
}

export const useTagsViewStore = defineStore('tagsView', () => {
  const visitedViews = ref<TagView[]>([])
  const cachedViews = ref<string[]>([])

  const cachedViewNames = computed(() => cachedViews.value)

  /** 添加 View */
  function addView(view: TagView) {
    const exists = visitedViews.value.some(v => v.path === view.path)
    if (exists)
      return

    visitedViews.value.push({
      ...view,
      title: view.meta?.title || 'no-name',
      path: view.path,
      name: view.name,
    })

    // 路由 meta.keepAlive 才缓存
    if (view.meta?.keepAlive && view.name) {
      if (!cachedViews.value.includes(view.name as string)) {
        cachedViews.value.push(view.name as string)
      }
    }
  }

  /** 删除 View */
  function delView(view: TagView) {
    const idx = visitedViews.value.findIndex(v => v.path === view.path)
    if (idx === -1)
      return

    visitedViews.value.splice(idx, 1)

    if (view.name) {
      const cacheIdx = cachedViews.value.indexOf(view.name as string)
      if (cacheIdx > -1) {
        cachedViews.value.splice(cacheIdx, 1)
      }
    }
  }

  /** 删除 Others Views */
  function delOthersViews(view: TagView) {
    visitedViews.value = visitedViews.value.filter(v => v.path === view.path || v.meta?.affix)
    cachedViews.value = cachedViews.value.filter(name => visitedViews.value.some(v => v.name === name))
  }

  /** 删除 All Views */
  function delAllViews() {
    const affixTags = visitedViews.value.filter(v => v.meta?.affix)
    visitedViews.value = affixTags
    cachedViews.value = []
  }

  /** 删除 Cached View */
  function delCachedView(name: string) {
    const idx = cachedViews.value.indexOf(name)
    if (idx > -1) {
      cachedViews.value.splice(idx, 1)
    }
  }

  return {
    visitedViews,
    cachedViews,
    cachedViewNames,
    addView,
    delView,
    delOthersViews,
    delAllViews,
    delCachedView,
  }
})
