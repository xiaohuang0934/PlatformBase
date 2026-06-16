<script setup lang="ts">
import type { UserDto } from '@/types/user'
import { ElMessage } from 'element-plus'
import { onActivated, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as userApi from '@/api/users'
import { parseTime } from '@/utils/index'
import MobileFilterBar, { type FilterItemConfig } from '@/components/MobileFilterBar.vue'

const router = useRouter()
const loading = ref(false)
const refreshing = ref(false)
const list = ref<UserDto[]>([])
const keyword = ref('')
const finished = ref(false)

/** 筛选条件值 */
const filterValues = reactive<Record<string, any>>({
  isActive: undefined,
})

/** 平铺展示的筛选项 */
const filterItems = ref<FilterItemConfig[]>([
  {
    key: 'isActive',
    title: '状态',
    type: 'select',
    options: [
      { label: '启用', value: true },
      { label: '禁用', value: false },
    ],
  },
])

/** 分页 */
const pageIndex = ref(1)
const pageSize = 10

/** 获取 List */
async function fetchList() {
  loading.value = true
  try {
    const res = await userApi.getUserList({
      keyword: keyword.value || undefined,
      isActive: filterValues.isActive,
      pageIndex: pageIndex.value,
      pageSize,
    })
    const items = res.data.items
    if (pageIndex.value === 1) list.value = items
    else list.value.push(...items)
    finished.value = items.length < pageSize
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

function onSearch() { pageIndex.value = 1; fetchList() }
function onFilterChange() { pageIndex.value = 1; fetchList() }
function onLoad() { pageIndex.value++; fetchList() }
async function onRefresh() {
  refreshing.value = true
  pageIndex.value = 1
  list.value = []
  finished.value = false
  await fetchList()
  refreshing.value = false
}
function goEdit(id: string) { router.push(`/m/users/${id}/edit`) }

onMounted(fetchList)
onActivated(() => { if (list.value.length > 0) fetchList() })
</script>

<template>
  <div class="m-page">
    <MobileFilterBar
      v-model="filterValues"
      v-model:keyword="keyword"
      :items="filterItems"
      @search="onSearch"
      @filter-change="onFilterChange"
    />

    <div class="m-toolbar">
      <van-button type="primary" block round to="/m/users/create">添加</van-button>
    </div>

    <div v-if="list.length === 0 && !loading" class="m-empty">
      <span class="m-empty__icon">📋</span>
      <span class="m-empty__text">暂无数据</span>
    </div>

    <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
      <van-list v-model:loading="loading" :finished="finished" finished-text="没有更多了" @load="onLoad">
        <div class="m-card-list">
          <div v-for="item in list" :key="item.id" class="m-card-list__item" @click="goEdit(item.id)">
            <div class="card-header">
              <span class="card-header__title">{{ item.username }} <van-icon name="arrow" size="14" color="var(--color-text-dim)" /></span>
              <van-tag :type="item.isActive ? 'success' : 'danger'" size="medium">
                {{ item.isActive ? '启用' : '禁用' }}
              </van-tag>
            </div>
            <div class="card-row">
              <span class="card-row__label">邮箱</span><span>{{ item.email || '-' }}</span>
            </div>
            <div class="card-row">
              <span class="card-row__label">角色</span><span>{{ (item.roles || []).join(' / ') || '-' }}</span>
            </div>
            <div class="card-row">
              <span class="card-row__label">创建</span><span>{{ parseTime(item.createdAt) }}</span>
            </div>
          </div>
        </div>
      </van-list>
    </van-pull-refresh>
  </div>
</template>

<style scoped lang="scss">
.m-toolbar { padding: 0 $spacing-base $spacing-sm; }
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
  &__title { font-size: $font-size-md; font-weight: 600; color: $color-text-primary; }
}
.card-row {
  display: flex;
  gap: 8px;
  font-size: $font-size-sm;
  color: $color-text-regular;
  padding: 2px 0;
  &__label { color: $color-text-dim; min-width: 40px; }
}
</style>
