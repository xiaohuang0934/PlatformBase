<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import * as fileApi from '@/api/files'
import { downloadFile } from '@/utils/download'
import { parseTime } from '@/utils/index'

const loading = ref(false)
const list = ref<any[]>([])
const searched = ref(false)
const query = reactive({ bizType: '', bizId: '' })

/** 获取 List */
async function fetchList() {
  if (!query.bizType || !query.bizId) { ElMessage.warning('请输入业务类型和业务ID'); return }
  loading.value = true; searched.value = true
  try { const res = await fileApi.getFileList({ bizType: query.bizType, bizId: query.bizId }); list.value = res.data ?? [] }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** Download */
function handleDownload(row: any) { downloadFile(`/api/v1/files/${row.id}/download`, row.fileName) }
</script>

<template>
  <div class="m-page">
    <div style="padding:8px 12px;display:flex;gap:8px">
      <van-field v-model="query.bizType" placeholder="业务类型" style="flex:1" />
      <van-field v-model="query.bizId" placeholder="业务ID" style="flex:1" />
      <van-button size="small" type="primary" @click="fetchList">
        查询
      </van-button>
    </div>
    <div v-if="searched" class="m-card-list">
      <div v-for="item in list" :key="item.id" class="m-card-list__item">
        <div class="card-row">
          <span class="card-row__label">{{ item.fileName }}</span>
        </div>
        <div class="card-row">
          <span class="card-row__label">大小</span><span class="card-row__value">{{ item.fileSize }}</span>
        </div>
        <div class="card-row">
          <span class="card-row__label">时间</span><span class="card-row__value">{{ parseTime(item.createdAt) }}</span>
        </div>
        <div style="margin-top:8px">
          <van-button size="small" type="primary" @click="handleDownload(item)">
            下载
          </van-button>
        </div>
      </div>
    </div>
    <div v-if="!searched" class="m-empty">
      <span class="m-empty__icon">📁</span><span class="m-empty__text">输入条件查询文件</span>
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
