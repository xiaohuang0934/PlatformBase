<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import * as paramApi from '@/api/system-params'
import FormDialog from '@/components/FormDialog.vue'
import { useCrudList } from '@/composables/useCrudList'
import { useDeleteConfirm } from '@/composables/useDeleteConfirm'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const { confirmDelete } = useDeleteConfirm()

const { loading, list, total, query, fetchList, onSearch, onReset, onPageChange } = useCrudList<any>(
  () => paramApi.getParamList({ keyword: query.keyword || undefined, pageIndex: query.pageIndex, pageSize: query.pageSize }),
)

const dialogVisible = ref(false)
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const form = reactive({ id: '', code: '', name: '', value: '', category: '', description: '', inheritable: true })
const submitting = ref(false)
const formRules: FormRules = {
  code: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  value: [{ required: true, message: '请输入值', trigger: 'blur' }],
}

function openCreate() { isEditing.value = false; Object.assign(form, { id: '', code: '', name: '', value: '', category: '', description: '', inheritable: true }); dialogVisible.value = true }
function openEdit(row: any) { isEditing.value = true; Object.assign(form, { id: row.id, code: row.code, value: row.value, category: row.category || '', description: row.description || '', inheritable: row.inheritable ?? true }); dialogVisible.value = true }

async function handleSubmit() {
  submitting.value = true
  try {
    if (isEditing.value) { await paramApi.updateParam(form.id, { value: form.value, description: form.description || undefined, inheritable: form.inheritable }); ElMessage.success('更新成功') }
    else { await paramApi.createParam({ name: form.name, code: form.code, value: form.value, category: form.category || undefined, description: form.description || undefined, inheritable: form.inheritable }); ElMessage.success('创建成功') }
    dialogVisible.value = false; fetchList()
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

function handleDelete(row: any) {
  confirmDelete('参数', row.code, () => paramApi.deleteParam(row.id), fetchList)
}

onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        系统参数
      </h2><el-button type="primary" @click="openCreate">
        新增参数
      </el-button>
    </div>
    <div class="search-bar">
      <el-input v-model="query.keyword" placeholder="编码 / 名称" clearable style="width: 200px" @keyup.enter="onSearch" />
      <el-button type="primary" @click="onSearch">
        搜索
      </el-button>
      <el-button @click="onReset">
        重置
      </el-button>
    </div>
    <el-table v-loading="loading" :data="list" border stripe row-key="id">
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="value" label="值" min-width="200" show-overflow-tooltip />
      <el-table-column prop="category" label="分类" width="120" />
      <el-table-column prop="inheritable" label="可继承" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="row.inheritable ? 'success' : 'info'" size="small">{{ row.inheritable ? '是' : '否' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" @click="openEdit(row)">
            编辑
          </el-button>
        </template>
      </el-table-column>
    </el-table>
    <div style="display:flex;justify-content:flex-end;margin-top:16px">
      <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" :total="total" :page-sizes="[10, 20, 50]" layout="total,sizes,prev,pager,next" @current-change="onPageChange" @size-change="onPageChange" />
    </div>
  </div>

  <FormDialog v-model="dialogVisible" :title="isEditing ? '编辑参数' : '新增参数'" :submitting="submitting" @confirm="handleSubmit" @closed="formRef?.resetFields()">
    <el-form ref="formRef" :model="form" :rules="formRules" label-width="80px">
      <el-form-item label="编码" prop="code">
        <el-input v-model="form.code" :disabled="isEditing" placeholder="请输入编码" />
      </el-form-item>
      <el-form-item label="值" prop="value">
        <el-input v-model="form.value" placeholder="请输入值" />
      </el-form-item>
      <el-form-item label="分类">
        <el-input v-model="form.category" placeholder="请输入分类" />
      </el-form-item>
      <el-form-item label="描述">
        <el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入描述" />
      </el-form-item>
      <el-form-item label="可继承">
        <el-switch v-model="form.inheritable" active-text="允许租户覆盖" inactive-text="仅平台级" />
      </el-form-item>
    </el-form>
  </FormDialog>
</template>
