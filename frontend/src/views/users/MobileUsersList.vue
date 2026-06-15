<script setup lang="ts">
import type { UserDto } from '@/types/user'
import { ElMessage } from 'element-plus'
import { onActivated, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as userApi from '@/api/users'
import { parseTime } from '@/utils/index'

const router = useRouter()
const loading = ref(false)
const list = ref<UserDto[]>([])
const query = reactive({ keyword: '', isActive: undefined as boolean | undefined, pageIndex: 1, pageSize: 10 })
const activeTab = ref(0)
const finished = ref(false)

function getIsActive(tab: number): boolean | undefined {
  if (tab === 1) return true
  if (tab === 2) return false
  return undefined
}

async function fetchList() {
  loading.value = true
  try {
    const res = await userApi.getUserList({ keyword: query.keyword || undefined, isActive: query.isActive, pageIndex: query.pageIndex, pageSize: query.pageSize })
    const items = res.data.items
    if (query.pageIndex === 1) list.value = items
    else list.value.push(...items)
    finished.value = items.length < query.pageSize
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

function onSearch() { query.pageIndex = 1; fetchList() }
function onFilterChange(tab: number) {
  activeTab.value = tab
  query.isActive = getIsActive(tab)
  query.pageIndex = 1
  fetchList()
}
function onLoad() { query.pageIndex++; fetchList() }
function goDetail(id: string) { router.push(`/m/users/${id}/edit`) }

onMounted(fetchList)
onActivated(() => { if (list.value.length > 0) fetchList() })
</script>

<template>
  <div class="m-page">
    <van-sticky>
      <van-search v-model="query.keyword" placeholder="搜索用户名/邮箱" shape="round" @search="onSearch" @clear="onSearch" />
      <van-tabs v-model="activeTab" :style="{ '--van-tab-font-size': '13px' }" @change="onFilterChange">
        <van-tab title="全部" :name="0" />
        <van-tab title="启用" :name="1" />
        <van-tab title="禁用" :name="2" />
      </van-tabs>
    </van-sticky>

    <div class="m-toolbar">
      <van-button type="primary" block round to="/m/users/create">
        添加
      </van-button>
    </div>

    <div v-if="list.length === 0 && !loading" class="m-empty">
      <span class="m-empty__icon">📋</span>
      <span class="m-empty__text">暂无数据</span>
    </div>

    <van-list v-model:loading="loading" :finished="finished" finished-text="没有更多了" @load="onLoad">
      <div class="m-card-list">
        <div v-for="item in list" :key="item.id" class="m-card-list__item" @click="goDetail(item.id)">
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
  </div>
</template>

<style scoped lang="scss">
.m-toolbar {
  padding: 8px 12px;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
  &__title {
    font-size: $font-size-md;
    font-weight: 600;
    color: $color-text-primary;
  }
}
.card-row {
  display: flex;
  gap: 8px;
  font-size: $font-size-sm;
  color: $color-text-regular;
  padding: 2px 0;
  &__label {
    color: $color-text-dim;
    min-width: 40px;
  }
}
</style>

.card-footer { display: flex; align-items: center; justify-content: center; gap: 4px; margin-top: 10px; padding-top: 8px; border-top: 1px solid var(--color-border); } .card-footer__link { font-size: 12px; color: var(--color-text-dim); }
