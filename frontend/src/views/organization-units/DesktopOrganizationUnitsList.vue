<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, provide, reactive, ref } from 'vue'
import * as orgApi from '@/api/organization'
import OrgTreeNode from './OrgTreeNode.vue'
import TenantSelector from '@/components/TenantSelector.vue'
import { useAuthStore } from '@/stores/auth'

interface TenantNode {
  tenantId: string
  tenantName: string
  tenantCode: string
  hasChildren: boolean
}

interface OrgNode {
  id: string
  name: string
  code: string
  parentId: string | null
  sortOrder: number
  hasChildren: boolean
}

const auth = useAuthStore()
const loading = ref(false)
const tenantList = ref<TenantNode[]>([])
const expandedTenantIds = ref<Set<string>>(new Set())
const expandedOrgIds = ref<Set<string>>(new Set())
const tenantOrgs = ref<Record<string, OrgNode[]>>({})

const keyword = ref('')

const filteredTenants = computed(() => {
  if (!keyword.value) return tenantList.value
  const kw = keyword.value.toLowerCase()
  return tenantList.value.filter(t =>
    t.tenantName.toLowerCase().includes(kw) || t.tenantCode.toLowerCase().includes(kw),
  )
})

async function loadTenants() {
  loading.value = true
  try {
    const res = await orgApi.getOrgNodes()
    tenantList.value = (res.data || []) as TenantNode[]
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

async function toggleTenant(tid: string) {
  if (expandedTenantIds.value.has(tid)) {
    expandedTenantIds.value.delete(tid)
  }
  else {
    expandedTenantIds.value.add(tid)
    if (!tenantOrgs.value[tid]) {
      try {
        const res = await orgApi.getOrgNodes({ tenantId: tid })
        tenantOrgs.value[tid] = (res.data || []) as OrgNode[]
      }
      catch { ElMessage.error('加载部门失败') }
    }
  }
  expandedTenantIds.value = new Set(expandedTenantIds.value)
}

function toggleOrg(tid: string, org: OrgNode) {
  if (expandedOrgIds.value.has(org.id)) {
    expandedOrgIds.value.delete(org.id)
  }
  else {
    expandedOrgIds.value.add(org.id)
    const key = `${tid}_${org.id}`
    if (!tenantOrgs.value[key]) {
      orgApi.getOrgNodes({ tenantId: tid, parentId: org.id }).then(res => {
        tenantOrgs.value[key] = (res.data || []) as OrgNode[]
        tenantOrgs.value = { ...tenantOrgs.value }
      }).catch(() => {})
    }
  }
  expandedOrgIds.value = new Set(expandedOrgIds.value)
}

// ───── 共享给递归子组件的 actions ─────
provide('orgActions', { tenantOrgs, expandedOrgIds, toggleOrg, openCreate, openEdit, handleDelete })

// ───── 新增/编辑 ─────
const dialogVisible = ref(false)
const dialogTitle = ref('新增部门')
const isEditing = ref(false)
const isChildCreate = ref(false)
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive({
  id: '',
  name: '',
  code: '',
  tenantId: '' as string | undefined,
  parentId: '' as string | undefined,
})
const rules: FormRules = {
  name: [{ required: true, message: '请输入部门名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入部门编码', trigger: 'blur' }],
}

const showParentSelector = computed(() => isEditing.value || isChildCreate.value)

/** 获取指定租户下已加载的部门选项（用于上级选择器） */
function getTenantOrgOptions(tid: string) {
  const result: { label: string, value: string }[] = []
  const roots = tenantOrgs.value[tid] || []
  for (const o of roots) {
    result.push({ label: o.name, value: o.id })
    addChildOptions(tid, o, 1, result)
  }
  return result
}

function addChildOptions(tid: string, org: OrgNode, depth: number, result: { label: string, value: string }[]) {
  if (!expandedOrgIds.value.has(org.id)) return
  const key = `${tid}_${org.id}`
  const children = tenantOrgs.value[key] || []
  for (const child of children) {
    result.push({ label: `${'─'.repeat(depth)} ${child.name}`, value: child.id })
    addChildOptions(tid, child, depth + 1, result)
  }
}

function openCreate(tenantId: string, parentId?: string) {
  isEditing.value = false
  isChildCreate.value = !!parentId
  dialogTitle.value = parentId ? '新增子部门' : '新增部门'
  Object.assign(form, { id: '', name: '', code: '', tenantId, parentId: parentId || undefined })
  dialogVisible.value = true
}

function openEdit(tid: string, org: OrgNode) {
  isEditing.value = true
  isChildCreate.value = false
  dialogTitle.value = '编辑部门'
  Object.assign(form, { id: org.id, name: org.name, code: org.code, tenantId: tid, parentId: org.parentId || undefined })
  dialogVisible.value = true
}

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    if (isEditing.value) {
      await orgApi.updateOrgUnit(form.id, { name: form.name, parentId: form.parentId || undefined })
      ElMessage.success('更新成功')
    }
    else {
      await orgApi.createOrgUnit({
        name: form.name,
        code: form.code,
        tenantId: form.tenantId,
        parentId: form.parentId || undefined,
      })
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    refreshTenantOrgs(form.tenantId)
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

function handleDelete(tid: string, org: OrgNode) {
  ElMessageBox.confirm(`确定删除部门 "${org.name}" 吗？`, '确认删除', {
    confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning',
  }).then(() => {
    orgApi.deleteOrgUnit(org.id).then(() => {
      ElMessage.success('已删除')
      refreshTenantOrgs(tid)
    }).catch(() => ElMessage.error('删除失败'))
  }).catch(() => {})
}

function refreshTenantOrgs(tid: string | undefined) {
  if (!tid) { loadTenants(); return }
  orgApi.getOrgNodes({ tenantId: tid }).then(res => {
    tenantOrgs.value[tid] = (res.data || []) as OrgNode[]
    for (const key of Object.keys(tenantOrgs.value)) {
      if (key.startsWith(`${tid}_`)) delete tenantOrgs.value[key]
    }
    tenantOrgs.value = { ...tenantOrgs.value }
  }).catch(() => {})
}

function onSearch() { /* filteredTenants 自动响应 */ }
function onReset() { keyword.value = '' }

onMounted(loadTenants)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        组织架构
      </h2>
    </div>

    <div class="search-bar">
      <el-input v-model="keyword" placeholder="搜索租户名称/编码" clearable style="width: 240px" @keyup.enter="onSearch" />
      <el-button type="primary" @click="onSearch">
        搜索
      </el-button>
      <el-button @click="onReset">
        重置
      </el-button>
    </div>

    <div v-loading="loading" class="org-tree">
      <template v-for="t in filteredTenants" :key="t.tenantId">
        <div class="tree-tenant">
          <div class="tree-tenant__row" @click="!t.hasChildren ? undefined : toggleTenant(t.tenantId)">
            <span class="tree-tenant__expand">
              <span v-if="t.hasChildren" class="tree-tenant__arrow">
                {{ expandedTenantIds.has(t.tenantId) ? '▼' : '▶' }}
              </span>
            </span>
            <span class="tree-tenant__name">{{ t.tenantName }}</span>
            <span class="tree-tenant__code">{{ t.tenantCode }}</span>
            <div class="tree-tenant__actions">
              <el-button type="primary" link size="small" :icon="Plus" @click.stop="openCreate(t.tenantId)">
                新增部门
              </el-button>
            </div>
          </div>

          <template v-if="expandedTenantIds.has(t.tenantId)">
            <OrgTreeNode
              v-for="org in (tenantOrgs[t.tenantId] || [])"
              :key="org.id"
              :tenant-id="t.tenantId"
              :org="org"
              :depth="1"
            />
          </template>
        </div>
      </template>

      <div v-if="!loading && filteredTenants.length === 0" class="org-tree__empty">
        暂无数据
      </div>
    </div>
  </div>

  <!-- 新增/编辑弹窗 -->
  <el-dialog v-model="dialogVisible" :title="dialogTitle" width="480px" destroy-on-close @closed="formRef?.resetFields()">
    <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
      <el-form-item v-if="!isEditing && auth.isPlatformAdmin && !form.tenantId" label="租户" prop="tenantId" :rules="[{ required: true, message: '请选择租户', trigger: 'change' }]">
        <TenantSelector v-model="form.tenantId" style="width: 100%" />
      </el-form-item>
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" placeholder="请输入部门名称" />
      </el-form-item>
      <el-form-item label="编码" prop="code">
        <el-input v-model="form.code" placeholder="请输入部门编码" />
      </el-form-item>
      <el-form-item v-if="showParentSelector" label="上级部门">
        <el-select v-model="form.parentId" placeholder="无（一级部门）" clearable style="width: 100%">
          <el-option v-for="p in getTenantOrgOptions(form.tenantId!)" :key="p.value" :label="p.label" :value="p.value" />
        </el-select>
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
</template>

<style scoped lang="scss">
.org-tree {
  background: $color-bg-card;
  border: 1px solid $color-border;
  border-radius: $radius-lg;
  padding: $spacing-sm 0;

  &__empty {
    text-align: center;
    padding: 40px 0;
    color: $color-text-dim;
    font-size: $font-size-md;
  }
}

.tree-tenant {
  &__row {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
    padding: 12px $spacing-md;
    cursor: pointer;
    transition: background $transition-fast;
    border-bottom: 1px solid $color-border;

    &:hover {
      background: $color-bg-hover;
    }
  }

  &__expand {
    width: 20px;
    flex-shrink: 0;
  }

  &__arrow {
    font-size: 10px;
    color: $color-text-dim;
  }

  &__name {
    font-weight: 600;
    font-size: $font-size-md;
    color: $color-text-primary;
  }

  &__code {
    font-size: $font-size-sm;
    color: $color-text-dim;
  }

  &__actions {
    margin-left: auto;
    display: flex;
    gap: 4px;
    flex-shrink: 0;
  }
}
</style>
