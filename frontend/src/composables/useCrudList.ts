import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'

/**
 * 通用 CRUD 列表组合式函数
 * 封装 loading / 分页查询 / 搜索重置 / 分页切换 等重复样板代码
 *
 * @param fetchFn 获取数据的异步函数，接收 query 参数，返回 { items, totalCount }
 *
 * @example
 * const { loading, list, total, query, fetchList, onSearch, onReset, onPageChange } = useCrudList(
 *   () => userApi.getUserList({ keyword: query.keyword, pageIndex: query.pageIndex, pageSize: query.pageSize })
 * )
 */
export function useCrudList<T>(
  fetchFn: () => Promise<{ data: { items: T[], totalCount: number } }>,
) {
  const loading = ref(false)
  const list = ref<T[]>([])
  const total = ref(0)
  const query = reactive({
    keyword: '',
    pageIndex: 1,
    pageSize: 10,
  })

  async function fetchList() {
    loading.value = true
    try {
      const res = await fetchFn()
      list.value = res.data.items
      total.value = res.data.totalCount
    }
    catch {
      ElMessage.error('加载失败，请重试')
    }
    finally {
      loading.value = false
    }
  }

  function onSearch() {
    query.pageIndex = 1
    fetchList()
  }

  function onReset() {
    query.keyword = ''
    query.pageIndex = 1
    fetchList()
  }

  function onPageChange(page: number) {
    query.pageIndex = page
    fetchList()
  }

  return { loading, list, total, query, fetchList, onSearch, onReset, onPageChange }
}
