import { ElMessage, ElMessageBox } from 'element-plus'

/**
 * 删除确认组合式函数 — 封装 ElMessageBox.confirm + 删除 + 刷新 样板代码
 *
 * @example
 * const { confirmDelete } = useDeleteConfirm()
 * await confirmDelete('用户', row.username, () => userApi.deleteUser(row.id), fetchList)
 */
export function useDeleteConfirm() {
  async function confirmDelete(
    label: string,
    name: string,
    deleteFn: () => Promise<any>,
    onSuccess?: () => void,
  ) {
    try {
      await ElMessageBox.confirm(
        `确定删除${label} "${name}" 吗？`,
        '确认删除',
        { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' },
      )
    }
    catch {
      return
    }

    try {
      await deleteFn()
      ElMessage.success('已删除')
      onSuccess?.()
    }
    catch {
      ElMessage.error('删除失败')
    }
  }

  return { confirmDelete }
}
