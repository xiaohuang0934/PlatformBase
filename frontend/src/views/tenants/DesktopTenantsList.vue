<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import * as tenantApi from '@/api/tenants'
import TableToolbar from '@/components/TableToolbar.vue'
import { useTableSelection } from '@/composables/useTableSelection'
import { useAuthStore } from '@/stores/auth'
import { parseTime } from '@/utils/index'

const loading = ref(false)
const list = ref<any[]>([])
const tableRef = ref<any>(null); const sel = useTableSelection<any>(tableRef)
const auth = useAuthStore()
const total = ref(0)
const query = reactive({ keyword: '', pageIndex: 1, pageSize: 10 })

/** 获取 List */
async function fetchList() {
  loading.value = true
  try {
    const res = await tenantApi.getTenantList({ keyword: query.keyword || undefined, pageIndex: query.pageIndex, pageSize: query.pageSize })
    list.value = res.data.items ?? []; total.value = res.data.totalCount ?? 0
  }
  catch {
    ElMessage.error('加载失败，请重试')
  }
  finally { loading.value = false }
}
/** 搜索 */
function onSearch() { query.pageIndex = 1; fetchList() }
/** On Re设置 */
function onReset() { query.keyword = ''; query.pageIndex = 1; fetchList() }

/** 批量删除 */
async function batchDelete() {
  try { await ElMessageBox.confirm(`确定删除选中的 ${sel.selectedCount.value} 个租户吗？`, '批量删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  loading.value = true
  try {
    await Promise.all(sel.selectedIds.value.map((id: string) => tenantApi.deleteTenant(id)))
    ElMessage.success('批量删除完成'); sel.clearSelection(); fetchList()
  }
  catch {
    ElMessage.error('操作失败，请重试')
  }
  finally { loading.value = false }
}

const dialogVisible = ref(false)
const dialogTitle = ref('新增租户')
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive({ id: '', name: '', code: '', description: '' })
const rules: FormRules = { name: [{ required: true, message: '请输入租户名称', trigger: 'blur' }], code: [{ required: true, message: '请输入租户编码', trigger: 'blur' }] }

/** 打开 Create */
function openCreate() { isEditing.value = false; dialogTitle.value = '新增租户'; Object.assign(form, { id: '', name: '', code: '', description: '' }); dialogVisible.value = true }
/** 打开 Edit */
function openEdit(row: any) { isEditing.value = true; dialogTitle.value = '编辑租户'; Object.assign(form, { id: row.id, name: row.name, code: row.code, description: row.description || '' }); dialogVisible.value = true }

/** Submit */
async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid)
    return; submitting.value = true
  try {
    if (isEditing.value) { await tenantApi.updateTenant(form.id, { name: form.name, description: form.description || undefined }); ElMessage.success('更新成功') }
    else { await tenantApi.createTenant({ name: form.name, code: form.code, description: form.description || undefined }); ElMessage.success('创建成功') }
    dialogVisible.value = false; fetchList()
  }
  catch {
    ElMessage.error('操作失败，请重试')
  }
  finally { submitting.value = false }
}

/** Delete */
async function handleDelete(row: any) {
  try { await ElMessageBox.confirm(`确定删除租户 "${row.name}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await tenantApi.deleteTenant(row.id); ElMessage.success('已删除'); fetchList()
}

/** 分页切换 */
function onPageChange(p: number) { query.pageIndex = p; fetchList() }
onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        租户管理
      </h2>
    </div>

    <div class="search-bar">
      <el-input v-model="query.keyword" placeholder="名称 / 编码" clearable style="width: 200px" @keyup.enter="onSearch" />
      <el-button type="primary" @click="onSearch">
        搜索
      </el-button>
      <el-button @click="onReset">
        重置
      </el-button>
    </div>

    <TableToolbar :selected-count="sel.selectedCount.value">
      <template #actions>
        <el-button type="primary" @click="openCreate">
          新增租户
        </el-button>
        <el-button :disabled="!sel.hasSelection.value" type="danger" plain @click="batchDelete">
          批量删除
        </el-button>
      </template>
    </TableToolbar>

    <el-table ref="tableRef" v-loading="loading" :data="list" border stripe row-key="id" @selection-change="sel.handleSelectionChange" @row-click="sel.toggleRow">
      <el-table-column type="selection" width="45" />
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="name" label="租户名称" min-width="140" />
      <el-table-column prop="code" label="编码" width="140" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="创建时间" width="160">
        <template #default="{ row }">
          {{ parseTime(row.createdAt) }}
        </template>
      </el-table-column>
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

  <el-dialog v-model="dialogVisible" :title="dialogTitle" width="480px" destroy-on-close @closed="formRef?.resetFields()">
    <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" placeholder="请输入租户名称" />
      </el-form-item>
      <el-form-item label="编码" prop="code">
        <el-input v-model="form.code" :disabled="isEditing" placeholder="请输入租户编码" />
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
