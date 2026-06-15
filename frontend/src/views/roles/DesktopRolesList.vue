<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import type { CreateRoleDto, RoleDto } from '@/types/auth'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import * as roleApi from '@/api/roles'
import FormDialog from '@/components/FormDialog.vue'
import TableToolbar from '@/components/TableToolbar.vue'
import TenantSelector from '@/components/TenantSelector.vue'
import { useAuthStore } from '@/stores/auth'
import { useCrudList } from '@/composables/useCrudList'
import { useDeleteConfirm } from '@/composables/useDeleteConfirm'
import { useTableSelection } from '@/composables/useTableSelection'
import { parseTime } from '@/utils/index'

const auth = useAuthStore()
const { confirmDelete } = useDeleteConfirm()
const queryExt = reactive({ isSystem: undefined as boolean | undefined, tenantIds: [] as string[] })

const { loading, list: roleList, total, query, fetchList, onSearch, onReset, onPageChange } = useCrudList<RoleDto>(
  () => roleApi.getRoleList({
    keyword: query.keyword || undefined,
    isSystem: queryExt.isSystem,
    pageIndex: query.pageIndex,
    pageSize: query.pageSize,
    tenantIds: queryExt.tenantIds.length ? queryExt.tenantIds : undefined,
  }),
)

function handleReset() { queryExt.isSystem = undefined; queryExt.tenantIds = []; onReset() }

const tableRef = ref<any>(null)
const sel = useTableSelection<RoleDto>(tableRef)

function onTenantChange(tid: string | null) {
  queryExt.tenantIds = tid ? [tid] : []
  onSearch()
}

async function batchDelete() {
  try {
    await Promise.all(sel.selectedIds.value.map(id => roleApi.deleteRole(id)))
    ElMessage.success('批量删除完成'); sel.clearSelection(); fetchList()
  }
  catch { ElMessage.error('操作失败') }
}

// ─── 表单 ───
const dialogVisible = ref(false)
const dialogTitle = ref('新增角色')
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive<CreateRoleDto & { id?: string }>({ name: '', code: '', description: '', tenantId: undefined })
const formRules: FormRules = {
  name: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入角色编码', trigger: 'blur' }],
}

function openCreate() {
  isEditing.value = false; dialogTitle.value = '新增角色'
  Object.assign(form, { id: '', name: '', code: '', description: '', tenantId: undefined })
  dialogVisible.value = true
}

function openEdit(row: RoleDto) {
  isEditing.value = true; dialogTitle.value = '编辑角色'
  Object.assign(form, { id: row.id, name: row.name, code: row.code, description: row.description || '', tenantId: undefined })
  dialogVisible.value = true
}

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    if (isEditing.value) {
      await roleApi.updateRole(form.id!, { name: form.name, description: form.description || undefined })
      ElMessage.success('更新成功')
    }
    else {
      await roleApi.createRole({ name: form.name, code: form.code, description: form.description || undefined, tenantId: form.tenantId || null })
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false; fetchList()
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

function handleDelete(row: RoleDto) {
  confirmDelete('角色', row.name, () => roleApi.deleteRole(row.id), fetchList)
}

onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">角色管理</h2>
    </div>

    <div class="search-bar">
      <el-input v-model="query.keyword" placeholder="角色名称 / 编码" clearable style="width: 200px" @keyup.enter="onSearch" />
      <el-select v-model="queryExt.isSystem" placeholder="类型" clearable style="width: 120px">
        <el-option label="系统角色" :value="true" />
        <el-option label="自定义" :value="false" />
      </el-select>
      <TenantSelector :model-value="queryExt.tenantIds[0] || null" style="width: 200px" @update:model-value="onTenantChange" />
      <el-button type="primary" @click="onSearch">搜索</el-button>
      <el-button @click="handleReset">重置</el-button>
    </div>

    <TableToolbar :selected-count="sel.selectedCount.value">
      <template #actions>
        <el-button type="primary" @click="openCreate">新增角色</el-button>
        <el-button :disabled="!sel.hasSelection.value" type="danger" plain @click="batchDelete">批量删除</el-button>
      </template>
    </TableToolbar>

    <el-table ref="tableRef" v-loading="loading" :data="roleList" border stripe row-key="id" @selection-change="sel.handleSelectionChange" @row-click="sel.toggleRow">
      <el-table-column type="selection" width="45" />
      <el-table-column v-if="auth.isPlatformAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="name" label="角色名称" min-width="140" />
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="类型" width="100" align="center">
        <template #default="{ row }"><el-tag :type="row.isSystem ? 'info' : ''" size="small">{{ row.isSystem ? '系统' : '自定义' }}</el-tag></template>
      </el-table-column>
      <el-table-column label="创建时间" width="160">
        <template #default="{ row }">{{ parseTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" @click="openEdit(row)">编辑</el-button>
          <el-button type="danger" link size="small" :disabled="row.isSystem" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div style="display:flex;justify-content:flex-end;margin-top:16px">
      <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" :total="total" :page-sizes="[10, 20, 50]" layout="total,sizes,prev,pager,next" @current-change="onPageChange" @size-change="onPageChange" />
    </div>
  </div>

  <FormDialog v-model="dialogVisible" :title="dialogTitle" :submitting="submitting" @confirm="handleSubmit" @closed="formRef?.resetFields()">
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
      <el-form-item v-if="auth.isPlatformAdmin" label="租户">
        <TenantSelector v-model="form.tenantId" style="width:100%" />
        <div style="color: var(--el-text-color-secondary); font-size: 12px; margin-top: 4px">留空 = 全局角色，对所有租户可见</div>
      </el-form-item>
    </el-form>
  </FormDialog>
</template>
