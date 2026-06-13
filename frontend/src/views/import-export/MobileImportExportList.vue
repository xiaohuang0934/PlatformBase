<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { downloadFile } from '@/utils/download'

async function handleExportUsers() { await downloadFile('/api/v1/import-export/users', 'users.xlsx') }
function handleImportUsers() { ElMessage.info('导入功能开发中') }
</script>

<template>
  <div class="m-page">
    <div class="m-card-list">
      <div class="m-card-list__item" style="text-align:center;padding:24px" @click="handleExportUsers">
        <p style="font-size:28px;margin-bottom:8px">
          📥
        </p>
        <p style="font-weight:600">
          导出用户
        </p>
        <p style="color:var(--color-text-dim);font-size:12px;margin-top:4px">
          导出为 Excel 文件
        </p>
      </div>
      <div class="m-card-list__item" style="text-align:center;padding:24px" @click="handleImportUsers">
        <p style="font-size:28px;margin-bottom:8px">
          📤
        </p>
        <p style="font-weight:600">
          导入用户
        </p>
        <p style="color:var(--color-text-dim);font-size:12px;margin-top:4px">
          批量导入 Excel / CSV
        </p>
      </div>
    </div>
  </div>
</template>
