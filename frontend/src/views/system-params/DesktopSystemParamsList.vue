<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import * as paramApi from '@/api/system-params'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const loading = ref(false)
const list = ref<any[]>([])
const total = ref(0)
const query = reactive({ keyword: '', pageIndex: 1, pageSize: 10 })

async function fetchList() {
  loading.value = true
  try {
    const res = await paramApi.getParamList({ keyword: query.keyword || undefined, pageIndex: query.pageIndex, pageSize: query.pageSize })
    list.value = res.data.items ?? []; total.value = res.data.totalCount ?? 0
  }
  catch {
    ElMessage.error('加载失败，请重试')
  }
  finally { loading.value = false }
}
function onSearch() { query.pageIndex = 1; fetchList() }
function onReset() { query.keyword = ''; query.pageIndex = 1; fetchList() }

const dialogVisible = ref(false)
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const form = reactive({ id: '', code: '', value: '', category: '', description: '' })
const submitting = ref(false)
const formRules: FormRules = {
  code: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  value: [{ required: true, message: '请输入值', trigger: 'blur' }],
}

function openCreate() { isEditing.value = false; Object.assign(form, { id: '', code: '', value: '', category: '', description: '' }); dialogVisible.value = true }
function openEdit(row: any) { isEditing.value = true; Object.assign(form, { id: row.id, code: row.code, value: row.value, category: row.category || '', description: row.description || '' }); dialogVisible.value = true }

async function handleSubmit() {
  submitting.value = true
  try {
    if (isEditing.value) { await paramApi.updateParam(form.id, { value: form.value, description: form.description || undefined }); ElMessage.success('更新成功') }
    else { await paramApi.createParam({ code: form.code, value: form.value, category: form.category || undefined, description: form.description || undefined }); ElMessage.success('创建成功') }
    dialogVisible.value = false; fetchList()
  }
  catch {
    ElMessage.error('操作失败，请重试')
  }
  finally { submitting.value = false }
}
async function handleDelete(row: any) {
  try { await ElMessageBox.confirm(`确定删除参数 "${row.code}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await paramApi.deleteParam(row.id); ElMessage.success('已删除'); fetchList()
}

function onPageChange(p: number) { query.pageIndex = p; fetchList() }
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
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" @click="openEdit(row)">
            编辑
          </el-button>
          <el-button type="danger" link size="small" @click="handleDelete(row)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>
    <div style="display:flex;justify-content:flex-end;margin-top:16px">
      <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" :total="total" :page-sizes="[10, 20, 50]" layout="total,sizes,prev,pager,next" @current-change="onPageChange" @size-change="onPageChange" />
    </div>
  </div>

  <el-dialog v-model="dialogVisible" :title="isEditing ? '编辑参数' : '新增参数'" width="480px" destroy-on-close @closed="formRef?.resetFields()">
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
    </el-form>
    <template #footer>
      <el-button @click="dialogVisible = false">
        取消
      </el-button><el-button type="primary" :loading="submitting" @click="handleSubmit">
        确定
      </el-button>
    </template>
  </el-dialog>
</template>
