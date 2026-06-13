<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { downloadFile } from '@/utils/download'

/** Export Users */
async function handleExportUsers() {
  await downloadFile('/api/v1/import-export/users', 'users.xlsx')
}

/** Import Users */
function handleImportUsers() { ElMessage.info('导入功能开发中') }
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        导入导出
      </h2>
    </div>
    <el-row :gutter="20">
      <el-col :span="8">
        <div class="page-container" style="text-align:center;padding:32px">
          <p style="font-size:32px;margin-bottom:16px">
            📥
          </p>
          <h3 style="margin-bottom:12px">
            导出用户
          </h3>
          <p style="color:#8892b0;margin-bottom:16px;font-size:13px">
            导出所有用户数据为 Excel 文件
          </p>
          <el-button type="primary" @click="handleExportUsers">
            下载 Excel
          </el-button>
        </div>
      </el-col>
      <el-col :span="8">
        <div class="page-container" style="text-align:center;padding:32px">
          <p style="font-size:32px;margin-bottom:16px">
            📤
          </p>
          <h3 style="margin-bottom:12px">
            导入用户
          </h3>
          <p style="color:#8892b0;margin-bottom:16px;font-size:13px">
            批量导入用户数据 Excel / CSV
          </p>
          <el-button @click="handleImportUsers">
            上传文件
          </el-button>
        </div>
      </el-col>
    </el-row>
  </div>
</template>
