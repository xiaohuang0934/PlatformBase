<script setup lang="ts">
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { computed, inject } from 'vue'

interface OrgNode {
  id: string
  name: string
  code: string
  parentId: string | null
  sortOrder: number
  hasChildren: boolean
}

interface OrgActions {
  tenantOrgs: Record<string, OrgNode[]>
  expandedOrgIds: Set<string>
  toggleOrg: (tid: string, org: OrgNode) => void
  openCreate: (tid: string, parentId?: string) => void
  openEdit: (tid: string, org: OrgNode) => void
  handleDelete: (tid: string, org: OrgNode) => void
}

const props = defineProps<{
  tenantId: string
  org: OrgNode
  depth: number
}>()

const actions = inject<OrgActions>('orgActions')!

const childrenKey = computed(() => `${props.tenantId}_${props.org.id}`)
const children = computed(() => actions.tenantOrgs[childrenKey.value] || [])
const isExpanded = computed(() => actions.expandedOrgIds.has(props.org.id))

const indentStyle = computed(() => ({
  paddingLeft: `${16 + props.depth * 24}px`,
}))

function onToggle() {
  if (!props.org.hasChildren) return
  actions.toggleOrg(props.tenantId, props.org)
}

function onAddChild(e: Event) {
  e.stopPropagation()
  actions.openCreate(props.tenantId, props.org.id)
}

function onEdit(e: Event) {
  e.stopPropagation()
  actions.openEdit(props.tenantId, props.org)
}

function onDelete(e: Event) {
  e.stopPropagation()
  actions.handleDelete(props.tenantId, props.org)
}
</script>

<template>
  <div>
    <div class="tree-item" @click="onToggle">
      <div class="tree-item__row" :style="indentStyle">
        <span class="tree-item__expand">
          <span v-if="org.hasChildren" class="tree-item__arrow">
            {{ isExpanded ? '▼' : '▶' }}
          </span>
        </span>
        <span class="tree-item__name">{{ org.name }}</span>
        <span class="tree-item__code">{{ org.code }}</span>
        <div class="tree-item__actions">
          <el-button type="primary" link size="small" :icon="Edit" @click="onEdit">
            编辑
          </el-button>
          <el-button type="danger" link size="small" :icon="Delete" @click="onDelete">
            删除
          </el-button>
          <el-button type="primary" link size="small" :icon="Plus" @click="onAddChild">
            子部门
          </el-button>
        </div>
      </div>
    </div>

    <template v-if="isExpanded">
      <OrgTreeNode
        v-for="child in children"
        :key="child.id"
        :tenant-id="tenantId"
        :org="child"
        :depth="depth + 1"
      />
    </template>
  </div>
</template>

<style scoped lang="scss">
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
