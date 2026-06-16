<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getOrgUnitTree } from '@/api/organization'

/** 树节点数据结构 */
export interface TreeNode {
  id: string
  name: string
  code?: string
  children?: TreeNode[]
  [key: string]: any
}

/** 树属性配置 */
export interface TreeProps {
  children?: string
  label?: string
  value?: string
}

/** 组件 Props */
interface Props {
  /** 选中值 */
  modelValue: string[]
  /** 自定义数据源（传入则不使用默认 API） */
  dataSource?: TreeNode[]
  /** 加载函数（传入则覆盖默认加载逻辑） */
  loadData?: () => Promise<TreeNode[]>
  /** 树属性配置 */
  treeProps?: TreeProps
  /** 是否显示复选框 */
  showCheckbox?: boolean
  /** 是否严格模式（父子不关联） */
  checkStrictly?: boolean
  /** 租户 ID（可选，传入则只加载该租户的部门） */
  tenantId?: string
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: () => [],
  dataSource: undefined,
  loadData: undefined,
  treeProps: () => ({
    children: 'children',
    label: 'name',
    value: 'id',
  }),
  showCheckbox: true,
  checkStrictly: true,
})

const emit = defineEmits<{
  'update:modelValue': [value: string[]]
}>()

const treeData = ref<TreeNode[]>([])
const loading = ref(false)
const treeRef = ref<any>(null)

/** 加载树数据 */
async function loadTree() {
  loading.value = true
  try {
    if (props.dataSource) {
      treeData.value = props.dataSource
    }
    else if (props.loadData) {
      treeData.value = await props.loadData()
    }
    else {
      const res = await getOrgUnitTree(props.tenantId ? { tenantId: props.tenantId } : undefined)
      treeData.value = (res.data || []) as TreeNode[]
    }
  }
  catch {
    treeData.value = []
  }
  finally {
    loading.value = false
  }
}

/** 节点选中变化 */
function handleCheck(_node: any, checked: { checkedKeys: string[] }) {
  emit('update:modelValue', checked.checkedKeys)
}

onMounted(loadTree)
</script>

<template>
  <el-tree
    ref="treeRef"
    :data="treeData"
    :loading="loading"
    :props="treeProps"
    :show-checkbox="showCheckbox"
    node-key="id"
    :default-checked-keys="modelValue"
    :check-strictly="checkStrictly"
    :expand-on-click-node="false"
    @check="handleCheck"
  >
    <template #default="{ data }">
      <slot name="node" :data="data">
        <span>{{ data[treeProps.label || 'name'] }}</span>
      </slot>
    </template>
  </el-tree>
</template>
