import { ref } from 'vue'

/**
 * 树形展开/折叠状态管理组合式函数
 * 不强制数据加载逻辑，仅管理展开 ID 集合。
 *
 * @example
 * const { expandedIds, toggleExpand, isExpanded } = useTreeExpand()
 * // 点击节点时
 * toggleExpand(item.id, async () => {
 *   const res = await api.loadChildren(item.id)
 *   return res.data
 * })
 */
export function useTreeExpand() {
  const expandedIds = ref<Set<string>>(new Set())

  function isExpanded(id: string): boolean {
    return expandedIds.value.has(id)
  }

  function toggleExpand(id: string, loadFn?: () => Promise<any[] | void>) {
    if (expandedIds.value.has(id)) {
      expandedIds.value.delete(id)
    }
    else {
      expandedIds.value.add(id)
      loadFn?.()
    }
    expandedIds.value = new Set(expandedIds.value)
  }

  return { expandedIds, isExpanded, toggleExpand }
}
