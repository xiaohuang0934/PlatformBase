<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { getOrgUnitTree } from '@/api/organization'

interface OrgOption {
  id: string
  name: string
  children?: OrgOption[]
}

const props = defineProps<{
  modelValue: string[]
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string[]]
}>()

const treeData = ref<OrgOption[]>([])
const loading = ref(false)

async function loadTree() {
  loading.value = true
  try {
    const res = await getOrgUnitTree()
    treeData.value = (res.data || []) as OrgOption[]
  }
  catch {
    treeData.value = []
  }
  finally {
    loading.value = false
  }
}

function handleCheck(_node: any, checked: { checkedKeys: string[] }) {
  emit('update:modelValue', checked.checkedKeys)
}

onMounted(loadTree)
</script>

<template>
  <el-tree
    ref="treeRef"
    :data="treeData"
    :props="{ children: 'children', label: 'name', value: 'id' }"
    show-checkbox
    node-key="id"
    :default-checked-keys="modelValue"
    check-strictly
    :expand-on-click-node="false"
    v-bind="$attrs"
    @check="handleCheck"
  >
    <template #default="{ node, data }">
      <span>{{ data.name }}</span>
    </template>
  </el-tree>
</template>
