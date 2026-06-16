<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { computed, onActivated, onMounted, reactive, ref } from 'vue'
import * as orgApi from '@/api/organization'

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

const loading = ref(false)
const tenantList = ref<TenantNode[]>([])
const keyword = ref('')
const filteredTenants = computed(() => {
  if (!keyword.value)
    return tenantList.value
  const kw = keyword.value.toLowerCase()
  return tenantList.value.filter(t => t.tenantName.toLowerCase().includes(kw) || t.tenantCode.toLowerCase().includes(kw))
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

onMounted(loadTenants)
onActivated(() => {
  if (tenantList.value.length > 0)
    loadTenants()
})

// ─── 新增部门 action sheet ───
const showForm = ref(false)
const form = reactive({ tenantId: '', name: '', code: '', parentId: '' as string | undefined })
const submitting = ref(false)

function openCreate(tenantId: string, parentId?: string) {
  Object.assign(form, { tenantId, name: '', code: '', parentId: parentId || undefined })
  showForm.value = true
}

async function handleCreate() {
  if (!form.name || !form.code)
    return
  submitting.value = true
  try {
    await orgApi.createOrgUnit({ name: form.name, code: form.code, tenantId: form.tenantId, parentId: form.parentId })
    ElMessage.success('创建成功')
    showForm.value = false
    refreshCurrentOrgList()
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

// ─── 租户→部门钻取 ───
const currentTenantId = ref('')
const currentTenantName = ref('')
const showOrgList = ref(false)
const orgList = ref<OrgNode[]>([])
const orgLoading = ref(false)
const expandedOrgIds = ref<Set<string>>(new Set())
const childOrgs = ref<Record<string, OrgNode[]>>({})

async function openTenantOrgs(t: TenantNode) {
  currentTenantId.value = t.tenantId
  currentTenantName.value = t.tenantName
  showOrgList.value = true
  orgLoading.value = true
  try {
    const res = await orgApi.getOrgNodes({ tenantId: t.tenantId })
    orgList.value = (res.data || []) as OrgNode[]
  }
  catch { ElMessage.error('加载部门失败') }
  finally { orgLoading.value = false }
}

function backToTenants() {
  showOrgList.value = false
  expandedOrgIds.value = new Set()
  childOrgs.value = {}
}

async function refreshCurrentOrgList() {
  try {
    const res = await orgApi.getOrgNodes({ tenantId: currentTenantId.value })
    orgList.value = (res.data || []) as OrgNode[]
    childOrgs.value = {}
    expandedOrgIds.value = new Set()
  }
  catch { }
}

async function toggleOrg(org: OrgNode) {
  if (expandedOrgIds.value.has(org.id)) {
    expandedOrgIds.value.delete(org.id)
  }
  else {
    expandedOrgIds.value.add(org.id)
    const key = `${currentTenantId.value}_${org.id}`
    if (!childOrgs.value[key]) {
      try {
        const res = await orgApi.getOrgNodes({ tenantId: currentTenantId.value, parentId: org.id })
        childOrgs.value[key] = (res.data || []) as OrgNode[]
        childOrgs.value = { ...childOrgs.value }
      }
      catch { }
    }
  }
  expandedOrgIds.value = new Set(expandedOrgIds.value)
}

function getChildren(orgId: string): OrgNode[] {
  return childOrgs.value[`${currentTenantId.value}_${orgId}`] || []
}

// ─── 递归渲染辅助 ───
function renderNodes(orgs: OrgNode[], level: number): any[] {
  const result: any[] = []
  for (const org of orgs) {
    result.push({ ...org, _level: level })
    if (expandedOrgIds.value.has(org.id)) {
      const children = getChildren(org.id)
      result.push(...renderNodes(children, level + 1))
    }
  }
  return result
}

const renderedList = computed(() => renderNodes(orgList.value, 1))
</script>

<template>
  <!-- 租户列表视图 -->
  <div v-if="!showOrgList" class="m-page">
    <van-sticky>
      <van-search v-model="keyword" placeholder="搜索租户名称/编码" shape="round" @search="onSearch" @clear="onSearch" />
    </van-sticky>

    <div v-if="!loading && filteredTenants.length === 0" class="m-empty">
      <span class="m-empty__icon">📋</span>
      <span class="m-empty__text">暂无数据</span>
    </div>

    <div v-loading="loading" class="m-card-list">
      <div v-for="t in filteredTenants" :key="t.tenantId" class="m-card-list__item" @click="openTenantOrgs(t)">
        <div class="card-header">
          <span class="card-header__title">{{ t.tenantName }} <van-icon name="arrow" size="14" color="var(--color-text-dim)" /></span>
        </div>
        <div class="card-row">
          <span class="card-row__label">编码</span><span>{{ t.tenantCode }}</span>
        </div>
      </div>
    </div>
  </div>

  <!-- 部门列表视图 -->
  <div v-else class="m-page">
    <van-nav-bar :title="currentTenantName" left-arrow fixed placeholder @click-left="backToTenants">
      <template #right>
        <van-icon name="plus" size="20" @click="openCreate(currentTenantId)" />
      </template>
    </van-nav-bar>

    <div v-if="!orgLoading && orgList.length === 0" class="m-empty" style="margin-top:46px">
      <span class="m-empty__icon">📁</span>
      <span class="m-empty__text">暂无部门</span>
    </div>

    <div v-loading="orgLoading" class="m-card-list" style="margin-top:8px">
      <div
        v-for="node in renderedList" :key="node.id" class="m-card-list__item"
        :style="{ paddingLeft: `${12 + node._level * 16}px` }"
        @click="node.hasChildren ? toggleOrg(node) : undefined"
      >
        <div class="card-header">
          <span class="card-header__title">
            <span v-if="node.hasChildren" style="margin-right:6px;font-size:10px;color:var(--color-text-dim)">
              {{ expandedOrgIds.has(node.id) ? '▼' : '▶' }}
            </span>
            {{ node.name }}
          </span>
          <span class="card-header__code">{{ node.code }}</span>
        </div>
      </div>
    </div>
  </div>

  <!-- 新增部门 action sheet -->
  <van-action-sheet v-model:show="showForm" title="新增部门">
    <div style="padding:16px">
      <van-field v-model="form.name" label="名称" placeholder="请输入部门名称" />
      <van-field v-model="form.code" label="编码" placeholder="请输入部门编码" />
      <van-button round block type="primary" :loading="submitting" style="margin-top:16px" @click="handleCreate">
        确定
      </van-button>
    </div>
  </van-action-sheet>
</template>

<style scoped lang="scss">
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  &__title {
    font-size: $font-size-md;
    font-weight: 600;
    color: $color-text-primary;
    display: flex;
    align-items: center;
  }
  &__code {
    font-size: $font-size-sm;
    color: $color-text-dim;
  }
}
.card-row {
  display: flex;
  gap: 8px;
  font-size: $font-size-sm;
  color: $color-text-regular;
  padding: 2px 0;
  &__label {
    color: $color-text-dim;
    min-width: 40px;
  }
}
</style>
