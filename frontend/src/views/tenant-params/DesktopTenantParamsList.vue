<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { reactive, ref, watch } from 'vue'
import * as tenantParamApi from '@/api/tenant-params'
import FormDialog from '@/components/FormDialog.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface TenantParamItem {
  id: string
  code: string
  name: string
  value: string
  category: string
  description: string
  sortOrder: number
  isEnabled: boolean
  createdAt: string
}

interface TenantItem {
  id: string
  name: string
}

const loading = ref(false)
const list = ref<TenantParamItem[]>([])
const total = ref(0)
const query = reactive({ keyword: '', pageIndex: 1, pageSize: 10, selectedTenantId: '' })
const tenants = ref<TenantItem[]>([])
const tenantsLoading = ref(false)

const dialogVisible = ref(false)
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const form = reactive({ id: '', code: '', name: '', value: '', category: '', description: '' })
const submitting = ref(false)
const formRules: FormRules = {
  code: [{ required: true, message: '请输入参数编码', trigger: 'blur' }],
  value: [{ required: true, message: '请输入参数值', trigger: 'blur' }],
}

/** 平台管理员加载租户列表 */
async function loadTenants() {
  if (!auth.isPlatformAdmin) return
  tenantsLoading.value = true
  try {
    const res = await tenantParamApi.getAllTenants()
    if (res.success) {
      tenants.value = res.data.items || []
      if (tenants.value.length > 0 && !query.selectedTenantId)
        query.selectedTenantId = tenants.value[0].id
    }
  }
  catch { /* ignore */ }
  finally { tenantsLoading.value = false }
}

/** 查询列表 */
async function fetchList() {
  loading.value = true
  try {
    const res = await tenantParamApi.getPaged({
      tenantId: query.selectedTenantId || undefined,
      keyword: query.keyword || undefined,
      pageIndex: query.pageIndex,
      pageSize: query.pageSize,
    })
    if (res.success) {
      list.value = res.data.items
      total.value = res.data.totalCount
    }
  }
  catch { ElMessage.error('查询失败') }
  finally { loading.value = false }
}

function onSearch() { query.pageIndex = 1; fetchList() }
function onReset() { query.keyword = ''; query.pageIndex = 1; fetchList() }
function onPageChange() { fetchList() }

watch(() => query.selectedTenantId, () => { query.pageIndex = 1; fetchList() })

function openCreate() {
  isEditing.value = false
  Object.assign(form, { id: '', code: '', name: '', value: '', category: '', description: '' })
  dialogVisible.value = true
}

function openEdit(row: TenantParamItem) {
  isEditing.value = true
  Object.assign(form, {
    id: row.id, code: row.code, name: row.name, value: row.value,
    category: row.category || '', description: row.description || '',
  })
  dialogVisible.value = true
}

async function handleSubmit() {
  submitting.value = true
  try {
    if (isEditing.value)
      await tenantParamApi.update(form.id, { value: form.value, description: form.description || undefined })
    else
      await tenantParamApi.create({ code: form.code, value: form.value, category: form.category || undefined, description: form.description || undefined })
    ElMessage.success(isEditing.value ? '更新成功' : '创建成功')
    dialogVisible.value = false
    fetchList()
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

async function handleDelete(row: TenantParamItem) {
  try {
    await ElMessageBox.confirm(`确定删除参数 "${row.code}" 的租户覆盖值吗？`, '确认删除', { type: 'warning' })
    await tenantParamApi.remove(row.id)
    ElMessage.success('删除成功')
    fetchList()
  }
  catch { /* cancel */ }
}

import { ElMessageBox } from 'element-plus'

onMounted(async () => {
  await loadTenants()
  fetchList()
})
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">租户参数</h2>
      <el-button type="primary" @click="openCreate">新增参数</el-button>
    </div>

    <div class="search-bar">
      <el-select
        v-if="auth.isPlatformAdmin"
        v-model="query.selectedTenantId"
        placeholder="选择租户"
        :loading="tenantsLoading"
        clearable
        style="width: 200px"
      >
        <el-option v-for="t in tenants" :key="t.id" :label="t.name" :value="t.id" />
      </el-select>
      <el-input v-model="query.keyword" placeholder="编码 / 名称" clearable style="width: 200px" @keyup.enter="onSearch" />
      <el-button type="primary" @click="onSearch">搜索</el-button>
      <el-button @click="onReset">重置</el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe row-key="id">
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="name" label="名称" min-width="120" />
      <el-table-column prop="value" label="覆盖值" min-width="200" show-overflow-tooltip />
      <el-table-column prop="category" label="分类" width="120" />
      <el-table-column prop="description" label="描述" min-width="180" show-overflow-tooltip />
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" @click="openEdit(row)">编辑</el-button>
          <el-button type="danger" link size="small" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div style="display:flex;justify-content:flex-end;margin-top:16px">
      <el-pagination
        v-model:current-page="query.pageIndex"
        v-model:page-size="query.pageSize"
        :total="total"
        :page-sizes="[10, 20, 50]"
        layout="total,sizes,prev,pager,next"
        @current-change="onPageChange"
        @size-change="onPageChange"
      />
    </div>
  </div>

  <FormDialog v-model="dialogVisible" :title="isEditing ? '编辑租户参数' : '新增租户参数'" :submitting="submitting" @confirm="handleSubmit" @closed="formRef?.resetFields()">
    <el-form ref="formRef" :model="form" :rules="formRules" label-width="80px">
      <el-form-item label="编码" prop="code">
        <el-input v-model="form.code" :disabled="isEditing" placeholder="请输入系统参数编码" />
      </el-form-item>
      <el-form-item label="值" prop="value">
        <el-input v-model="form.value" placeholder="请输入覆盖值" />
      </el-form-item>
      <el-form-item label="分类">
        <el-input v-model="form.category" placeholder="请输入分类" />
      </el-form-item>
      <el-form-item label="描述">
        <el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入描述" />
      </el-form-item>
    </el-form>
  </FormDialog>
</template>
