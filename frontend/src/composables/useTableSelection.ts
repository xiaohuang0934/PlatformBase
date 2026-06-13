import type { Ref } from 'vue'
import { computed, ref } from 'vue'

export function useTableSelection<T extends { id: string }>(tableRef?: Ref<any>) {
  const selectedRows = ref<T[]>([])
  const _tableRef = tableRef || ref<any>(null)

  const selectedIds = computed(() => selectedRows.value.map(r => r.id))
  const selectedCount = computed(() => selectedRows.value.length)
  const hasSelection = computed(() => selectedRows.value.length > 0)

  /** Selection Change */
  function handleSelectionChange(rows: T[]) {
    selectedRows.value = rows
  }

  /** Toggle Row */
  function toggleRow(row: T) {
    if (!_tableRef.value)
      return
    _tableRef.value.toggleRowSelection(row)
  }

  /** Clear Selection */
  function clearSelection() {
    _tableRef.value?.clearSelection()
  }

  return {
    selectedRows,
    selectedIds,
    selectedCount,
    hasSelection,
    tableRef: _tableRef,
    handleSelectionChange,
    toggleRow,
    clearSelection,
  }
}
