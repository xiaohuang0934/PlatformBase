<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import type { RoleDto } from '@/types/auth'
import type { CreateUserDto, OrgUnitNode, UserDto } from '@/types/user'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import { getRoleList } from '@/api/roles'
import * as userApi from '@/api/users'
import * as menuApi from '@/api/menus'
import OrgSelector from '@/components/OrgSelector.vue'
import TableToolbar from '@/components/TableToolbar.vue'
import TenantSelector from '@/components/TenantSelector.vue'
import { useTableSelection } from '@/composables/useTableSelection'
import { useAuthStore } from '@/stores/auth'
import { UserType } from '@/types/user'
import { parseTime } from '@/utils/index'

const loading = ref(false)
const userList = ref<UserDto[]>([])
const total = ref(0)
const tableRef = ref<any>(null)
const sel = useTableSelection<UserDto>(tableRef)
const auth = useAuthStore()

const query = reactive({
  keyword: '',
  isActive: undefined as boolean | undefined,
  pageIndex: 1,
  pageSize: 10,
  tenantIds: [] as string[],
})

const userTypeLabel: Record<number, string> = {
  [UserType.PlatformAdmin]: '平台管理员',
  [UserType.TenantAdmin]: '租户管理员',
  [UserType.TenantUser]: '租户用户',
}

const userTypeTag: Record<number, 'danger' | 'warning' | 'info'> = {
  [UserType.PlatformAdmin]: 'danger',
  [UserType.TenantAdmin]: 'warning',
  [UserType.TenantUser]: 'info',
}

const allRoles = ref<RoleDto[]>([])

const menuTree = ref<any[]>([])
const menuTreeRef = ref<any>(null)

async function loadRoles() {
  const res = await getRoleList({ pageSize: 200 })
  allRoles.value = res.data.items
}

async function loadMenuTree() {
  try {
    const res = await menuApi.getMenuTree()
    menuTree.value = res.data || []
  }
  catch { /* ignore */ }
}

async function fetchList() {
  loading.value = true
  try {
    const res = await userApi.getUserList({
      keyword: query.keyword || undefined,
      isActive: query.isActive,
      pageIndex: query.pageIndex,
      pageSize: query.pageSize,
      tenantIds: query.tenantIds.length ? query.tenantIds : undefined,
    })
    userList.value = res.data.items
    total.value = res.data.totalCount
  }
  catch {
    ElMessage.error('加载失败，请重试')
  }
  finally { loading.value = false }
}

function onSearch() { query.pageIndex = 1; fetchList() }
function onReset() { query.keyword = ''; query.isActive = undefined; query.tenantIds = []; query.pageIndex = 1; fetchList() }

function onTenantChange(tid: string | null) {
  query.tenantIds = tid ? [tid] : []
  onSearch()
}

function handleMenuCheck() {
  form.menuIds = menuTreeRef.value?.getCheckedKeys() || []
}

// 批量操作
async function batchDelete() {
  try { await ElMessageBox.confirm(`确定删除选中的 ${sel.selectedCount.value} 个用户吗？`, '批量删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  loading.value = true
  try {
    await Promise.all(sel.selectedIds.value.map(id => userApi.deleteUser(id)))
    ElMessage.success('批量删除完成'); sel.clearSelection(); fetchList()
  }
  catch { ElMessage.error('操作失败，请重试') }
  finally { loading.value = false }
}

async function batchToggle(isActive: boolean) {
  loading.value = true
  try {
    await Promise.all(sel.selectedRows.value.filter(r => r.isActive !== isActive).map(r => userApi.toggleUser(r.id)))
    ElMessage.success(isActive ? '批量启用完成' : '批量禁用完成')
    sel.clearSelection(); fetchList()
  }
  catch { ElMessage.error('操作失败，请重试') }
  finally { loading.value = false }
}

// 新增/编辑弹窗
const dialogVisible = ref(false)
const dialogTitle = ref('新增用户')
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const submitting = ref(false)

const form = reactive<CreateUserDto & { id?: string }>({
  username: '',
  password: '',
  email: '',
  phoneNumber: '',
  tenantId: undefined,
  userType: undefined,
  roleIds: [],
  organizationUnitIds: [],
  menuIds: [],
})

const defaultFormRules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
  roleIds: [{ required: true, message: '请选择角色', trigger: 'change', type: 'array', min: 1 }],
  organizationUnitIds: [{ required: true, message: '请选择部门', trigger: 'change', type: 'array', min: 1 }],
}
const formRules = reactive<FormRules>({ ...defaultFormRules })

const showTenantField = computed(() => auth.isPlatformAdmin && !isEditing.value)

function openCreate() {
  isEditing.value = false
  dialogTitle.value = '新增用户'
  Object.assign(form, {
    id: undefined,
    username: '',
    password: '',
    email: '',
    phoneNumber: '',
    tenantId: undefined,
    userType: undefined,
    roleIds: [],
    organizationUnitIds: [],
    menuIds: [],
  })
  formRules.password = { required: true, message: '请输入密码', trigger: 'blur' }
  formRules.roleIds = { required: true, message: '请选择角色', trigger: 'change', type: 'array', min: 1 }
  formRules.organizationUnitIds = { required: true, message: '请选择部门', trigger: 'change', type: 'array', min: 1 }
  dialogVisible.value = true
}

function openEdit(row: UserDto) {
  isEditing.value = true
  dialogTitle.value = '编辑用户'
  formRules.password = undefined as any
  formRules.roleIds = undefined as any
  formRules.organizationUnitIds = undefined as any
  Object.assign(form, {
    id: row.id,
    username: row.username,
    password: '',
    email: row.email || '',
    phoneNumber: row.phoneNumber || '',
    tenantId: undefined,
    userType: row.userType,
    roleIds: row.roles || [],
    organizationUnitIds: row.organizationUnits?.map((o: OrgUnitNode) => o.id) || [],
  })
  dialogVisible.value = true
}

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid)
    return
  submitting.value = true
  try {
    if (isEditing.value && form.id) {
      await userApi.updateUser(form.id, {
        email: form.email || undefined,
        phoneNumber: form.phoneNumber || undefined,
        userType: form.userType,
        roleIds: form.roleIds.length ? form.roleIds : undefined,
        organizationUnitIds: form.organizationUnitIds.length ? form.organizationUnitIds : undefined,
      })
      ElMessage.success('更新成功')
    }
    else {
      await userApi.createUser({
        username: form.username,
        password: form.password,
        email: form.email || undefined,
        phoneNumber: form.phoneNumber || undefined,
        tenantId: form.tenantId || undefined,
        userType: form.userType,
        roleIds: form.roleIds,
        organizationUnitIds: form.organizationUnitIds,
        menuIds: form.menuIds?.length ? form.menuIds : undefined,
      })
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    fetchList()
  }
  catch { ElMessage.error('操作失败，请重试') }
  finally { submitting.value = false }
}

// 删除/切换
async function handleDelete(row: UserDto) {
  try { await ElMessageBox.confirm(`确定删除用户 "${row.username}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await userApi.deleteUser(row.id); ElMessage.success('已删除'); fetchList()
}

async function handleToggle(row: UserDto) {
  await userApi.toggleUser(row.id)
  ElMessage.success(row.isActive ? '已禁用' : '已启用'); fetchList()
}

// 重置密码
const pwdDialogVisible = ref(false)
const pwdTargetUser = ref<UserDto | null>(null)
const newPassword = ref('')
function openResetPwd(row: UserDto) { pwdTargetUser.value = row; newPassword.value = ''; pwdDialogVisible.value = true }
async function handleResetPwd() {
  if (!newPassword.value || !pwdTargetUser.value)
    return
  await userApi.resetPassword(pwdTargetUser.value.id, newPassword.value)
  ElMessage.success('密码已重置'); pwdDialogVisible.value = false
}

// 分配菜单
const menuDialogVisible = ref(false)
const menuTargetUser = ref<UserDto | null>(null)
const selectedMenuIds = ref<string[]>([])
const assignMenuTreeRef = ref<any>(null)

function openAssignMenusForSelected() {
  if (sel.selectedCount.value === 0) {
    ElMessage.warning('请先选择用户')
    return
  }
  if (sel.selectedCount.value > 1) {
    ElMessage.warning('每次只能为一个用户分配菜单')
    return
  }
  const user = sel.selectedRows.value[0]
  openAssignMenus(user)
}

async function openAssignMenus(row: UserDto) {
  menuTargetUser.value = row
  selectedMenuIds.value = []
  try {
    const res = await menuApi.getUserMenus(row.id)
    selectedMenuIds.value = res.data || []
  }
  catch { /* ignore */ }
  menuDialogVisible.value = true
  // 等待 DOM 更新后设置选中状态
  setTimeout(() => {
    if (assignMenuTreeRef.value && selectedMenuIds.value.length > 0) {
      assignMenuTreeRef.value.setCheckedKeys(selectedMenuIds.value)
    }
  }, 100)
}

async function handleAssignMenus() {
  if (!menuTargetUser.value)
    return
  const checkedKeys = assignMenuTreeRef.value?.getCheckedKeys() || []
  const halfCheckedKeys = assignMenuTreeRef.value?.getHalfCheckedKeys() || []
  // 只保存叶子节点（页面和按钮），不保存目录节点
  const menuIds = checkedKeys.filter((id: string) => !halfCheckedKeys.includes(id))
  try {
    await menuApi.assignMenus(menuTargetUser.value.id, menuIds)
    ElMessage.success('菜单分配成功')
    menuDialogVisible.value = false
  }
  catch { ElMessage.error('分配失败，请重试') }
}

function onPageChange(p: number) { query.pageIndex = p; fetchList() }
function formatRoles(roles: string[]) { return roles?.join(' / ') || '-' }
function formatOrgs(orgs: OrgUnitNode[]) { return orgs?.map(o => o.name).join(' / ') || '-' }

onMounted(() => { loadRoles(); loadMenuTree(); fetchList() })
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        用户管理
      </h2>
    </div>

    <!-- 搜索栏 -->
    <div class="search-bar">
      <el-input v-model="query.keyword" placeholder="用户名 / 邮箱" clearable style="width: 200px" @keyup.enter="onSearch" />
      <el-select v-model="query.isActive" placeholder="状态" clearable style="width: 120px">
        <el-option label="启用" :value="true" />
        <el-option label="禁用" :value="false" />
      </el-select>
      <TenantSelector
        :model-value="query.tenantIds[0] || null"
        style="width: 200px"
        @update:model-value="onTenantChange"
      />
      <el-button type="primary" @click="onSearch">
        搜索
      </el-button>
      <el-button @click="onReset">
        重置
      </el-button>
    </div>

    <!-- 工具栏 -->
    <TableToolbar :selected-count="sel.selectedCount.value">
      <template #actions>
        <el-button type="primary" @click="openCreate">
          新增用户
        </el-button>
        <el-button type="success" @click="openAssignMenusForSelected">
          分配菜单
        </el-button>
        <el-button :disabled="!sel.hasSelection.value" @click="batchToggle(true)">
          批量启用
        </el-button>
        <el-button :disabled="!sel.hasSelection.value" @click="batchToggle(false)">
          批量禁用
        </el-button>
        <el-button :disabled="!sel.hasSelection.value" type="danger" plain @click="batchDelete">
          批量删除
        </el-button>
      </template>
    </TableToolbar>

    <!-- 表格 -->
    <el-table ref="tableRef" v-loading="loading" :data="userList" border stripe row-key="id" @selection-change="sel.handleSelectionChange" @row-click="sel.toggleRow">
      <el-table-column type="selection" width="45" />
      <el-table-column v-if="auth.isPlatformAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="username" label="用户名" min-width="120" />
      <el-table-column prop="email" label="邮箱" min-width="180" show-overflow-tooltip />
      <el-table-column prop="phoneNumber" label="手机" width="140" />
      <el-table-column label="类型" width="110" align="center">
        <template #default="{ row }">
          <el-tag :type="userTypeTag[row.userType] || 'info'" size="small">
            {{ userTypeLabel[row.userType] || row.userType }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="租户" min-width="120" show-overflow-tooltip>
        <template #default="{ row }">
          {{ row.tenantName || '-' }}
        </template>
      </el-table-column>
      <el-table-column label="状态" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'danger'" size="small">
            {{ row.isActive ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="角色" min-width="140" show-overflow-tooltip>
        <template #default="{ row }">
          {{ formatRoles(row.roles) }}
        </template>
      </el-table-column>
      <el-table-column label="部门" min-width="140" show-overflow-tooltip>
        <template #default="{ row }">
          {{ formatOrgs(row.organizationUnits) }}
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="160">
        <template #default="{ row }">
          {{ parseTime(row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" @click="openEdit(row)">
            编辑
          </el-button>
          <el-button type="warning" link size="small" @click="openResetPwd(row)">
            重置密码
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <div style="display:flex;justify-content:flex-end;margin-top:16px">
      <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" :total="total" :page-sizes="[10, 20, 50]" layout="total,sizes,prev,pager,next" @current-change="onPageChange" @size-change="onPageChange" />
    </div>

    <!-- 新增/编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="560px" destroy-on-close @closed="formRef?.resetFields()">
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="80px">
        <el-form-item label="用户名" prop="username">
          <el-input v-model="form.username" :disabled="isEditing" placeholder="请输入用户名" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="form.password" type="password" show-password :placeholder="isEditing ? '留空则不修改' : '请输入密码'" />
        </el-form-item>
        <el-form-item label="邮箱">
          <el-input v-model="form.email" placeholder="请输入邮箱" />
        </el-form-item>
        <el-form-item label="手机号">
          <el-input v-model="form.phoneNumber" placeholder="请输入手机号" />
        </el-form-item>
        <el-form-item v-if="showTenantField" label="目标租户" prop="tenantId" :rules="[{ required: true, message: '请选择租户', trigger: 'change' }]">
          <TenantSelector v-model="form.tenantId" style="width:100%" />
        </el-form-item>
        <el-form-item v-if="auth.isPlatformAdmin" label="用户类型">
          <el-select v-model="form.userType" placeholder="用户类型" style="width:100%">
            <el-option label="租户管理员" :value="UserType.TenantAdmin" />
            <el-option label="租户用户" :value="UserType.TenantUser" />
          </el-select>
        </el-form-item>
        <el-form-item label="角色" prop="roleIds">
          <el-select v-model="form.roleIds" multiple placeholder="请选择角色" style="width:100%">
            <el-option v-for="role in allRoles" :key="role.id" :label="role.name" :value="role.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="部门" prop="organizationUnitIds">
          <div style="max-height:200px;overflow-y:auto;border:1px solid var(--el-border-color);border-radius:4px;padding:8px">
            <OrgSelector v-model="form.organizationUnitIds" :tenant-id="form.tenantId" />
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">
          取消
        </el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          确定
        </el-button>
      </template>
    </el-dialog>

    <!-- 重置密码弹窗 -->
    <el-dialog v-model="pwdDialogVisible" title="重置密码" width="400px">
      <el-form label-width="80px">
        <el-form-item label="用户名">
          <span>{{ pwdTargetUser?.username }}</span>
        </el-form-item>
        <el-form-item label="新密码">
          <el-input v-model="newPassword" type="password" show-password placeholder="请输入新密码" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pwdDialogVisible = false">
          取消
        </el-button>
        <el-button type="warning" @click="handleResetPwd">
          确定
        </el-button>
      </template>
    </el-dialog>

    <!-- 分配菜单弹窗 -->
    <el-dialog v-model="menuDialogVisible" :title="`分配菜单 - ${menuTargetUser?.username}`" width="500px">
      <div style="max-height:400px;overflow-y:auto;border:1px solid var(--el-border-color);border-radius:4px;padding:8px">
        <el-tree
          ref="assignMenuTreeRef"
          :data="menuTree"
          show-checkbox
          node-key="id"
          :props="{ label: 'name', children: 'children' }"
          :check-strictly="false"
        />
      </div>
      <template #footer>
        <el-button @click="menuDialogVisible = false">
          取消
        </el-button>
        <el-button type="primary" @click="handleAssignMenus">
          确定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>
