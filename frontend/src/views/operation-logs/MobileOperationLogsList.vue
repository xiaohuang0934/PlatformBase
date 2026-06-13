<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import * as logApi from '@/api/operation-logs'
import { parseTime } from '@/utils/index'

const loading = ref(false)
const list = ref<any[]>([])
const query = reactive({ keyword: '', pageIndex: 1, pageSize: 10 })
const finished = ref(false)

/** 获取 List */
async function fetchList() {
  loading.value = true
  try {
    const res = await logApi.getLogList({ keyword: query.keyword || undefined, pageIndex: query.pageIndex, pageSize: query.pageSize })
    const items = res.data.items ?? []
    if (query.pageIndex === 1)
      list.value = items; else list.value.push(...items)
    finished.value = items.length < query.pageSize
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** 滚动加载更多 */
function onLoad() { query.pageIndex++; fetchList() }
onMounted(fetchList)
</script>

<template>
  <div class="m-page">
    <van-list v-model:loading="loading" :finished="finished" finished-text="没有更多了" @load="onLoad">
      <div class="m-card-list">
        <div v-for="item in list" :key="item.id" class="m-card-list__item">
          <div class="card-row">
            <span class="card-row__label">用户</span><span class="card-row__value">{{ item.username || '-' }}</span>
          </div>
          <div class="card-row">
            <span class="card-row__label">操作</span><span class="card-row__value">{{ item.action }}</span>
          </div>
          <div class="card-row">
            <span class="card-row__label">资源</span><span class="card-row__value">{{ item.resource }}</span>
          </div>
          <div class="card-row">
            <span class="card-row__label">时间</span><span class="card-row__value">{{ parseTime(item.timestamp) }}</span>
          </div>
        </div>
      </div>
    </van-list>
  </div>
</template>

<style scoped lang="scss">
.card-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 0;
  &__label {
    font-size: $font-size-sm;
    color: $color-text-dim;
  }
  &__value {
    font-size: $font-size-sm;
    color: $color-text-regular;
  }
}
</style>
