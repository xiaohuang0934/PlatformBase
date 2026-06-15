<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import * as orgApi from '@/api/organization'
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

/** 关键字搜索（前端过滤租户名称/编码） */
const keyword = ref('')

const filteredTenants = computed(() => {
  if (!keyword.value) return tenantList.value
  const kw = keyword.value.toLowerCase()
  return tenantList.value.filter(t =>
    t.tenantName.toLowerCase().includes(kw) || t.tenantCode.toLowerCase().includes(kw),
  )
})

/** 加载租户摘要列表 */
async function loadTenants() {
  loading.value = true
  try {
    const res = await orgApi.getOrgNodes()
    tenantList.value = (res.data || []) as TenantNode[]
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** 切换租户展开 */
async function toggleTenant(tid: string) {
  if (expandedTenantIds.value.has(tid)) {
    expandedTenantIds.value.delete(tid)
  }
  else {
    expandedTenantIds.value.add(tid)
    // 首次展开时加载该租户的一级部门
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

/** 切换部门展开（加载子级） */
async function toggleOrg(tid: string, org: OrgNode) {
  if (expandedOrgIds.value.has(org.id)) {
    expandedOrgIds.value.delete(org.id)
    expandedOrgIds.value = new Set(expandedOrgIds.value)
    return
  }
  expandedOrgIds.value.add(org.id)
  expandedOrgIds.value = new Set(expandedOrgIds.value)

  // 懒加载子部门
  const key = `${tid}_${org.id}`
  if (!tenantOrgs.value[key]) {
    try {
      const res = await orgApi.getOrgNodes({ tenantId: tid, parentId: org.id })
      tenantOrgs.value[key] = (res.data || []) as OrgNode[]
    }
    catch { ElMessage.error('加载子部门失败') }
  }
}

/** 获取缩进样式 */
function getOrgNameStyle(level: number) {
  return { paddingLeft: `${16 + level * 24}px` }
}

// ───── 新增/编辑 ─────
const dialogVisible = ref(false)
const dialogTitle = ref('新增部门')
const isEditing = ref(false)
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

/** 获取指定租户下的部门选项（用于上级选择） */
function getTenantOrgOptions(tid: string) {
  const result: { label: string, value: string }[] = []
  // 一级部门
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

/** 打开新增 */
function openCreate(tenantId?: string) {
  isEditing.value = false
  dialogTitle.value = '新增部门'
  Object.assign(form, { id: '', name: '', code: '', tenantId: tenantId || undefined, parentId: undefined })
  dialogVisible.value = true
}

/** 打开编辑 */
function openEdit(tid: string, org: OrgNode) {
  isEditing.value = true
  dialogTitle.value = '编辑部门'
  Object.assign(form, { id: org.id, name: org.name, code: org.code, tenantId: tid, parentId: org.parentId || undefined })
  dialogVisible.value = true
}

/** 提交 */
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
        parentId: form.parentId,
      })
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    refreshTenantOrgs(form.tenantId)
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

/** 删除 */
async function handleDelete(tid: string, org: OrgNode) {
  try { await ElMessageBox.confirm(`确定删除部门 "${org.name}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await orgApi.deleteOrgUnit(org.id)
  ElMessage.success('已删除')
  refreshTenantOrgs(tid)
}

/** 刷新指定租户的部门树 */
async function refreshTenantOrgs(tid: string | undefined) {
  if (!tid) { loadTenants(); return }
  try {
    const res = await orgApi.getOrgNodes({ tenantId: tid })
    tenantOrgs.value[tid] = (res.data || []) as OrgNode[]
    // 清除已展开子级的缓存（强制重新加载）
    for (const key of Object.keys(tenantOrgs.value)) {
      if (key.startsWith(`${tid}_`)) delete tenantOrgs.value[key]
    }
  }
  catch { /* 静默失败 */ }
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

    <!-- 搜索栏 -->
    <div class="search-bar">
      <el-input v-model="keyword" placeholder="搜索租户名称/编码" clearable style="width: 240px" @keyup.enter="onSearch" />
      <el-button type="primary" @click="onSearch">
        搜索
      </el-button>
      <el-button @click="onReset">
        重置
      </el-button>
    </div>

    <!-- 租户→部门树 -->
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

          <!-- 展开的部门列表 -->
          <template v-if="expandedTenantIds.has(t.tenantId)">
            <div v-for="org in (tenantOrgs[t.tenantId] || [])" :key="org.id">
              <div class="tree-item tree-item--level1" @click="!org.hasChildren ? undefined : toggleOrg(t.tenantId, org)">
                <div class="tree-item__row" :style="getOrgNameStyle(1)">
                  <span class="tree-item__expand">
                    <span v-if="org.hasChildren" class="tree-item__arrow">
                      {{ expandedOrgIds.has(org.id) ? '▼' : '▶' }}
                    </span>
                  </span>
                  <span class="tree-item__name">{{ org.name }}</span>
                  <span class="tree-item__code">{{ org.code }}</span>
                  <div class="tree-item__actions">
                    <el-button type="primary" link size="small" :icon="Edit" @click.stop="openEdit(t.tenantId, org)">
                      编辑
                    </el-button>
                    <el-button type="danger" link size="small" :icon="Delete" @click.stop="handleDelete(t.tenantId, org)">
                      删除
                    </el-button>
                    <el-button v-if="org.hasChildren" type="primary" link size="small" :icon="Plus" @click.stop="openCreate(t.tenantId)">
                      子部门
                    </el-button>
                  </div>
                </div>
              </div>

              <!-- level2：一级部门的子部门 -->
              <template v-if="expandedOrgIds.has(org.id)">
                <div v-for="child in (tenantOrgs[`${t.tenantId}_${org.id}`] || [])" :key="child.id">
                  <div class="tree-item tree-item--level2" @click="!child.hasChildren ? undefined : toggleOrg(t.tenantId, child)">
                    <div class="tree-item__row" :style="getOrgNameStyle(2)">
                      <span class="tree-item__expand">
                        <span v-if="child.hasChildren" class="tree-item__arrow">
                          {{ expandedOrgIds.has(child.id) ? '▼' : '▶' }}
                        </span>
                      </span>
                      <span class="tree-item__name">{{ child.name }}</span>
                      <span class="tree-item__code">{{ child.code }}</span>
                      <div class="tree-item__actions">
                        <el-button type="primary" link size="small" :icon="Edit" @click.stop="openEdit(t.tenantId, child)">
                          编辑
                        </el-button>
                        <el-button type="danger" link size="small" :icon="Delete" @click.stop="handleDelete(t.tenantId, child)">
                          删除
                        </el-button>
                      </div>
                    </div>
                  </div>

                  <!-- level3+：递归更深层级（此处只支持到 level3，可按需扩展） -->
                  <template v-if="expandedOrgIds.has(child.id)">
                    <div v-for="grandchild in (tenantOrgs[`${t.tenantId}_${child.id}`] || [])" :key="grandchild.id">
                      <div class="tree-item tree-item--level3">
                        <div class="tree-item__row" :style="getOrgNameStyle(3)">
                          <span class="tree-item__expand">
                            <span class="tree-item__arrow">
                              {{ expandedOrgIds.has(grandchild.id) ? '▼' : '▶' }}
                            </span>
                          </span>
                          <span class="tree-item__name">{{ grandchild.name }}</span>
                          <span class="tree-item__code">{{ grandchild.code }}</span>
                          <div class="tree-item__actions">
                            <el-button type="primary" link size="small" :icon="Edit" @click.stop="openEdit(t.tenantId, grandchild)">
                              编辑
                            </el-button>
                            <el-button type="danger" link size="small" :icon="Delete" @click.stop="handleDelete(t.tenantId, grandchild)">
                              删除
                            </el-button>
                          </div>
                        </div>
                      </div>
                    </div>
                  </template>
                </div>
              </template>
            </div>
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
      <el-form-item v-if="form.tenantId" label="上级部门">
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

.tree-item {
  &__row {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
    padding: 8px $spacing-md;
    transition: background $transition-fast;

    &:hover {
      background: $color-bg-hover;
    }
  }

  &--level1 &__row {
    padding-left: 40px;
  }

  &--level2 &__row {
    padding-left: 64px;
  }

  &--level3 &__row {
    padding-left: 88px;
  }

  &__expand {
    width: 20px;
    cursor: pointer;
    color: $color-text-dim;
    flex-shrink: 0;
  }

  &__arrow {
    font-size: 10px;
  }

  &__name {
    font-weight: 500;
    color: $color-text-primary;
    min-width: 80px;
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
