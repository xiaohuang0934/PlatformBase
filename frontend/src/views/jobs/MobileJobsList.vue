<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import * as jobApi from '@/api/jobs'

const loading = ref(false)
const list = ref<any[]>([])
/** 获取 List */
async function fetchList() {
  loading.value = true; try { const res = await jobApi.getJobList(); list.value = res.data ?? [] }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}
/** Trigger */
async function handleTrigger(row: any) { await jobApi.triggerJob(row.jobId); ElMessage.success('已手动触发') }
onMounted(fetchList)
</script>

<template>
  <div class="m-page">
    <div class="m-card-list">
      <div v-for="item in list" :key="item.jobId" class="m-card-list__item">
        <div class="card-row">
          <span class="card-row__label">{{ item.description || item.jobId }}</span>
          <el-tag :type="item.isRunning ? 'success' : 'info'" size="small">
            {{ item.isRunning ? '运行中' : '已停止' }}
          </el-tag>
        </div>
        <div v-if="item.cron" class="card-row">
          <span class="card-row__label">Cron</span><span class="card-row__value">{{ item.cron }}</span>
        </div>
        <div style="margin-top:8px">
          <van-button size="small" type="primary" @click="handleTrigger(item)">
            手动触发
          </van-button>
        </div>
      </div>
    </div>
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
    color: $color-text-regular;
  }
  &__value {
    font-size: $font-size-sm;
    color: $color-text-dim;
  }
}
</style>
