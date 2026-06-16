<script setup lang="ts">
import { Delete, RefreshRight, Search } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import * as logApi from '@/api/operation-logs'
import { useCrudList } from '@/composables/useCrudList'
import { useAuthStore } from '@/stores/auth'
import { parseTime } from '@/utils/index'

const auth = useAuthStore()

const { loading, list, total, query, fetchList, onSearch, onReset, onPageChange } = useCrudList<any>(
  () => logApi.getLogList({ keyword: query.keyword || undefined, pageIndex: query.pageIndex, pageSize: query.pageSize }),
)

async function handleCleanup() {
  await logApi.cleanupLogs(90)
  ElMessage.success('已清理 90 天前的日志')
  fetchList()
}

onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        操作日志
      </h2>
      <el-button :icon="Delete" @click="handleCleanup">
        清理旧日志
      </el-button>
    </div>
    <div class="search-bar">
      <el-input v-model="query.keyword" placeholder="用户名 / 操作" clearable style="width: 220px" @keyup.enter="onSearch" />
      <el-button type="primary" :icon="Search" @click="onSearch">
        搜索
      </el-button>
      <el-button :icon="RefreshRight" @click="onReset">
        重置
      </el-button>
    </div>
    <el-table v-loading="loading" :data="list" border stripe row-key="id">
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="username" label="用户" width="120" />
      <el-table-column prop="action" label="操作" width="120" />
      <el-table-column prop="resource" label="资源" min-width="160" show-overflow-tooltip />
      <el-table-column prop="detail" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column prop="ipAddress" label="IP" width="140" />
      <el-table-column label="时间" width="170">
        <template #default="{ row }">
          {{ parseTime(row.timestamp) }}
        </template>
      </el-table-column>
    </el-table>
    <div style="display:flex;justify-content:flex-end;margin-top:16px">
      <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" :total="total" :page-sizes="[10, 20, 50]" layout="total,sizes,prev,pager,next" @current-change="onPageChange" @size-change="onPageChange" />
    </div>
  </div>
</template>
