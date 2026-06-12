<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import type { RoleDto } from '@/types/auth'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import * as roleApi from '@/api/roles'
import TableToolbar from '@/components/TableToolbar.vue'
import { useTableSelection } from '@/composables/useTableSelection'
import { useAuthStore } from '@/stores/auth'
import { parseTime } from '@/utils/index'

const loading = ref(false)
const roleList = ref<RoleDto[]>([])
const total = ref(0)
const tableRef = ref<any>(null); const sel = useTableSelection<RoleDto>(tableRef)
const auth = useAuthStore()

const query = reactive({ keyword: '', isSystem: undefined as boolean | undefined, pageIndex: 1, pageSize: 10 })

async function fetchList() {
  loading.value = true
  try {
    const res = await roleApi.getRoleList({ keyword: query.keyword || undefined, isSystem: query.isSystem, pageIndex: query.pageIndex, pageSize: query.pageSize })
    roleList.value = res.data.items; total.value = res.data.totalCount
  }
  catch {
    ElMessage.error('加载失败，请重试')
  }
  finally { loading.value = false }
}
function onSearch() { query.pageIndex = 1; fetchList() }
function onReset() { query.keyword = ''; query.isSystem = undefined; query.pageIndex = 1; fetchList() }

async function batchDelete() {
  try { await ElMessageBox.confirm(`确定删除选中的 ${sel.selectedCount.value} 个角色吗？`, '批量删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  loading.value = true
  try {
    await Promise.all(sel.selectedIds.value.map(id => roleApi.deleteRole(id)))
    ElMessage.success('批量删除完成'); sel.clearSelection(); fetchList()
  }
  catch {
    ElMessage.error('操作失败，请重试')
  }
  finally { loading.value = false }
}
const dialogVisible = ref(false)
const dialogTitle = ref('新增角色')
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive({ id: '', name: '', code: '', description: '' })
const formRules: FormRules = { name: [{ required: true, message: '请输入角色名称', trigger: 'blur' }], code: [{ required: true, message: '请输入角色编码', trigger: 'blur' }] }

function openCreate() { isEditing.value = false; dialogTitle.value = '新增角色'; Object.assign(form, { id: '', name: '', code: '', description: '' }); dialogVisible.value = true }
function openEdit(row: RoleDto) { isEditing.value = true; dialogTitle.value = '编辑角色'; Object.assign(form, { id: row.id, name: row.name, code: row.code, description: row.description || '' }); dialogVisible.value = true }

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid)
    return; submitting.value = true
  try {
    if (isEditing.value) { await roleApi.updateRole(form.id, { name: form.name, description: form.description || undefined }); ElMessage.success('更新成功') }
    else { await roleApi.createRole({ name: form.name, code: form.code, description: form.description || undefined }); ElMessage.success('创建成功') }
    dialogVisible.value = false; fetchList()
  }
  catch {
    ElMessage.error('操作失败，请重试')
  }
  finally { submitting.value = false }
}

async function handleDelete(row: RoleDto) {
  try { await ElMessageBox.confirm(`确定删除角色 "${row.name}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await roleApi.deleteRole(row.id); ElMessage.success('已删除'); fetchList()
}

function onPageChange(p: number) { query.pageIndex = p; fetchList() }
onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        角色管理
      </h2>
    </div>

    <div class="search-bar">
      <el-input v-model="query.keyword" placeholder="角色名称 / 编码" clearable style="width: 200px" @keyup.enter="onSearch" />
      <el-select v-model="query.isSystem" placeholder="类型" clearable style="width: 120px">
        <el-option label="系统角色" :value="true" />
        <el-option label="自定义" :value="false" />
      </el-select>
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
          新增角色
        </el-button>
        <el-button :disabled="!sel.hasSelection.value" type="danger" plain @click="batchDelete">
          批量删除
        </el-button>
      </template>
    </TableToolbar>

    <el-table ref="tableRef" v-loading="loading" :data="roleList" border stripe row-key="id" @selection-change="sel.handleSelectionChange" @row-click="sel.toggleRow">
      <el-table-column type="selection" width="45" />
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="name" label="角色名称" min-width="140" />
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="类型" width="100" align="center">
        <template #default="{ row }">
          <el-tag :type="row.isSystem ? 'info' : ''" size="small">
            {{ row.isSystem ? '系统' : '自定义' }}
          </el-tag>
        </template>
      </el-table-column>
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
          <el-button type="danger" link size="small" :disabled="row.isSystem" @click="handleDelete(row)">
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
    <el-form ref="formRef" :model="form" :rules="formRules" label-width="80px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" placeholder="请输入角色名称" />
      </el-form-item>
      <el-form-item label="编码" prop="code">
        <el-input v-model="form.code" :disabled="isEditing" placeholder="请输入角色编码" />
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
