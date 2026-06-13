<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import * as jobApi from '@/api/jobs'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const loading = ref(false)
const list = ref<any[]>([])

/** 获取 List */
async function fetchList() {
  loading.value = true
  try { const res = await jobApi.getJobList(); list.value = res.data ?? [] }
  finally { loading.value = false }
}

/** Start */
async function handleStart(row: any) { await jobApi.startJob(row.jobId); ElMessage.success('已启动'); fetchList() }
/** Stop */
async function handleStop(row: any) { await jobApi.stopJob(row.jobId); ElMessage.success('已停止'); fetchList() }
/** Trigger */
async function handleTrigger(row: any) { await jobApi.triggerJob(row.jobId); ElMessage.success('已手动触发') }

const cronDialog = ref(false)
const cronTarget = ref<any>(null)
const cronValue = ref('')

/** 打开 Cron */
function openCron(row: any) { cronTarget.value = row; cronValue.value = row.cron || ''; cronDialog.value = true }
/** Cron Save */
async function handleCronSave() { await jobApi.updateJobCron(cronTarget.value.jobId, cronValue.value); ElMessage.success('Cron 已更新'); cronDialog.value = false; fetchList() }

onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        定时任务
      </h2>
    </div>
    <el-table v-loading="loading" :data="list" border stripe row-key="jobId">
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="jobId" label="任务 ID" min-width="160" show-overflow-tooltip />
      <el-table-column prop="description" label="描述" min-width="180" />
      <el-table-column label="状态" width="100" align="center">
        <template #default="{ row }">
          <el-tag :type="row.isRunning ? 'success' : 'info'" size="small">
            {{ row.isRunning ? '运行中' : '已停止' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="cron" label="Cron 表达式" width="150" />
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button v-if="!row.isRunning" type="success" size="small" @click="handleStart(row)">
            启动
          </el-button>
          <el-button v-else type="warning" size="small" @click="handleStop(row)">
            停止
          </el-button>
          <el-button size="small" @click="handleTrigger(row)">
            手动触发
          </el-button>
          <el-button size="small" @click="openCron(row)">
            Cron
          </el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
  <el-dialog v-model="cronDialog" title="修改 Cron 表达式" width="420px">
    <el-input v-model="cronValue" placeholder="如 0 */5 * * * *" />
    <template #footer>
      <el-button @click="cronDialog = false">
        取消
      </el-button><el-button type="primary" @click="handleCronSave">
        保存
      </el-button>
    </template>
  </el-dialog>
</template>
