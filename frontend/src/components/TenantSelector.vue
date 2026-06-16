<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { getAccessibleTenants } from '@/api/tenants'
import { useAuthStore } from '@/stores/auth'

/** 选项数据结构 */
export interface TenantOption {
  id: string
  name: string
  code: string
}

/** 组件 Props */
interface Props {
  /** 选中值 */
  modelValue: string | null
  /** 自定义数据源（传入则不使用默认 API） */
  dataSource?: TenantOption[]
  /** 加载函数（传入则覆盖默认加载逻辑） */
  loadData?: () => Promise<TenantOption[]>
  /** 是否可见（默认根据平台管理员判断） */
  visible?: boolean
  /** 占位文本 */
  placeholder?: string
  /** 组件宽度 */
  width?: string
  /** 是否可清空 */
  clearable?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  dataSource: undefined,
  loadData: undefined,
  visible: undefined,
  placeholder: '选择租户',
  width: '200px',
  clearable: true,
})

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
}>()

const auth = useAuthStore()
const options = ref<TenantOption[]>([])
const loading = ref(false)

/** 是否可见 */
const isVisible = computed(() => {
  if (props.visible !== undefined)
    return props.visible
  return auth.isPlatformAdmin
})

/** 加载选项数据 */
async function loadOptions() {
  loading.value = true
  try {
    if (props.dataSource) {
      options.value = props.dataSource
    }
    else if (props.loadData) {
      options.value = await props.loadData()
    }
    else {
      const res = await getAccessibleTenants()
      options.value = (res.data || []) as TenantOption[]
    }
  }
  catch {
    options.value = []
  }
  finally {
    loading.value = false
  }
}

/** 选项变化 */
function handleChange(val: string | null) {
  emit('update:modelValue', val)
  auth.setCurrentTenantId(val)
}

onMounted(() => {
  if (isVisible.value)
    loadOptions()
})

watch(() => auth.isLoggedIn, (val) => {
  if (val && isVisible.value)
    loadOptions()
})

watch(() => isVisible.value, (val) => {
  if (val && options.value.length === 0)
    loadOptions()
})
</script>

<template>
  <el-select
    v-if="isVisible"
    :model-value="modelValue"
    :loading="loading"
    :placeholder="placeholder"
    :clearable="clearable"
    :style="{ width }"
    @change="handleChange"
    @visible-change="(v: boolean) => v && loadOptions()"
  >
    <el-option
      v-for="t in options"
      :key="t.id"
      :label="t.name"
      :value="t.id"
    />
  </el-select>
</template>
