<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import * as fileApi from '@/api/files'
import { useDeleteConfirm } from '@/composables/useDeleteConfirm'
import { useAuthStore } from '@/stores/auth'
import { downloadFile } from '@/utils/download'
import { parseTime } from '@/utils/index'

const auth = useAuthStore()
const { confirmDelete } = useDeleteConfirm()
const loading = ref(false)
const list = ref<any[]>([])
const searched = ref(false)
const query = reactive({ bizType: '', bizId: '' })

async function fetchList() {
  if (!query.bizType || !query.bizId) { ElMessage.warning('请输入业务类型和业务ID'); return }
  loading.value = true
  searched.value = true
  try { const res = await fileApi.getFileList({ bizType: query.bizType, bizId: query.bizId }); list.value = res.data ?? [] }
  finally { loading.value = false }
}

function handleDelete(row: any) {
  confirmDelete('文件', row.fileName, () => fileApi.deleteFile(row.id), fetchList)
}

function handleDownload(row: any) { downloadFile(`/api/v1/files/${row.id}/download`, row.fileName) }
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        文件管理
      </h2>
    </div>
    <div class="search-bar">
      <el-input v-model="query.bizType" placeholder="业务类型" clearable style="width: 160px" @keyup.enter="fetchList" />
      <el-input v-model="query.bizId" placeholder="业务ID" clearable style="width: 280px" @keyup.enter="fetchList" />
      <el-button type="primary" @click="fetchList">
        查询
      </el-button>
    </div>
    <el-table v-if="searched" v-loading="loading" :data="list" border stripe row-key="id">
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="fileName" label="文件名" min-width="200" show-overflow-tooltip />
      <el-table-column prop="fileSize" label="大小" width="100" />
      <el-table-column prop="bizType" label="业务类型" width="120" />
      <el-table-column label="上传时间" width="170">
        <template #default="{ row }">
          {{ parseTime(row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" @click="handleDownload(row)">
            下载
          </el-button>
          <el-button type="danger" link size="small" @click="handleDelete(row)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>
    <div v-if="!searched" class="m-empty">
      <span class="m-empty__icon">📁</span>
      <span class="m-empty__text">请输入业务类型和业务ID查询文件</span>
    </div>
  </div>
</template>
