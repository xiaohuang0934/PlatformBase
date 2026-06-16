<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import type { CreateRoleDto, RoleDto } from '@/types/auth'
import { ElMessage } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'
import * as roleApi from '@/api/roles'
import * as permApi from '@/api/permissions'
import { getPermissions } from '@/api/auth'
import FormDialog from '@/components/FormDialog.vue'
import TableToolbar from '@/components/TableToolbar.vue'
import TenantSelector from '@/components/TenantSelector.vue'
import { useCrudList } from '@/composables/useCrudList'
import { useTableSelection } from '@/composables/useTableSelection'
import { useAuthStore } from '@/stores/auth'
import { parseTime } from '@/utils/index'

const auth = useAuthStore()
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

// ─── 权限选择 ───
interface GroupedPerm {
  group: string
  items: { code: string; name: string }[]
}
const allPermGroups = ref<GroupedPerm[]>([])
const selectedPermCodes = ref<string[]>([])
const permLoading = ref(false)

/** 加载权限列表（按当前用户拥有的权限过滤 + 分组） */
async function loadAllPermissions() {
  permLoading.value = true
  try {
    // 获取当前用户拥有的权限编码
    let userPermCodes: string[] = []
    try {
      const res = await getPermissions()
      userPermCodes = res.data || []
    } catch { /* 非登录态时使用空列表 */ }

    // 获取全部权限（分页取全部）
    const res = await permApi.getPermissionList({ pageIndex: 1, pageSize: 500 })
    const allPerms = res.data?.items || []

    // 过滤：平台管理员可见全部，租户用户只能看到自己拥有的
    const filtered = auth.isPlatformAdmin ? allPerms : allPerms.filter((p: any) => userPermCodes.includes(p.code))

    // 按 groupName 分组
    const groupMap = new Map<string, { code: string; name: string }[]>()
    for (const p of filtered) {
      const g = p.groupName || '其他'
      if (!groupMap.has(g)) groupMap.set(g, [])
      groupMap.get(g)!.push({ code: p.code, name: p.name })
    }

    allPermGroups.value = Array.from(groupMap.entries())
      .map(([group, items]) => ({ group, items }))
  }
  catch { /* ignore */ }
  finally { permLoading.value = false }
}

/** 切换权限选择 */
function togglePerm(code: string) {
  const idx = selectedPermCodes.value.indexOf(code)
  if (idx >= 0) selectedPermCodes.value.splice(idx, 1)
  else selectedPermCodes.value.push(code)
}

// ─── 打开表单 ───
function openCreate() {
  isEditing.value = false; dialogTitle.value = '新增角色'
  Object.assign(form, { id: '', name: '', code: '', description: '', tenantId: undefined })
  selectedPermCodes.value = []
  dialogVisible.value = true
}

async function openEdit(row: RoleDto) {
  isEditing.value = true; dialogTitle.value = '编辑角色'
  Object.assign(form, { id: row.id, name: row.name, code: row.code, description: row.description || '', tenantId: undefined })
  selectedPermCodes.value = []
  // 加载角色已有权限
  try {
    const res = await roleApi.getRolePermissions(row.id)
    selectedPermCodes.value = res.data || []
  }
  catch { /* ignore */ }
  dialogVisible.value = true
}

// ─── 提交 ───
async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    if (isEditing.value && form.id) {
      await roleApi.updateRole(form.id, { name: form.name, description: form.description || undefined })
      if (selectedPermCodes.value.length > 0)
        await roleApi.assignRolePermissions(form.id, selectedPermCodes.value)
      ElMessage.success('更新成功')
    }
    else {
      const res = await roleApi.createRole({
        name: form.name, code: form.code,
        description: form.description || undefined,
        tenantId: form.tenantId || null,
      })
      if (selectedPermCodes.value.length > 0) {
        const newRole: any = res.data
        await roleApi.assignRolePermissions(newRole.id, selectedPermCodes.value)
      }
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false; fetchList()
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

function handleDelete(row: RoleDto) {
  // 已从页面上移除删除按钮，保留方法兼容
}

onMounted(() => {
  fetchList()
  loadAllPermissions()
})
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
        <template #default="{ row }">
          <el-tag :type="row.isSystem ? 'info' : ''" size="small">{{ row.isSystem ? '系统' : '自定义' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="160">
        <template #default="{ row }">{{ parseTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="100" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" @click="openEdit(row)">编辑</el-button>
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
      <el-form-item label="权限">
        <div v-loading="permLoading" style="max-height:300px;overflow-y:auto;border:1px solid var(--el-border-color);border-radius:4px;padding:12px;width:100%">
          <div v-for="group in allPermGroups" :key="group.group" style="margin-bottom:8px">
            <div style="font-size:13px;font-weight:600;color:var(--el-text-color-primary);margin-bottom:4px">{{ group.group }}</div>
            <el-checkbox-group v-model="selectedPermCodes" size="small">
              <el-checkbox v-for="item in group.items" :key="item.code" :label="item.code" style="margin-right:12px;margin-bottom:4px">
                {{ item.name }}
              </el-checkbox>
            </el-checkbox-group>
          </div>
          <div v-if="allPermGroups.length === 0 && !permLoading" style="color:var(--el-text-color-secondary);font-size:13px">暂无可用权限</div>
        </div>
      </el-form-item>
    </el-form>
  </FormDialog>
</template>
