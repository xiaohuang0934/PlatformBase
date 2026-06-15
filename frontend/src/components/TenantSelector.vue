<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { getAccessibleTenants } from '@/api/tenants'
import { useAuthStore } from '@/stores/auth'

interface TenantOption {
  id: string
  name: string
  code: string
}

const props = defineProps<{
  modelValue: string | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string | null]
}>()

const auth = useAuthStore()
const tenants = ref<TenantOption[]>([])
const loading = ref(false)

const isVisible = computed(() => auth.isPlatformAdmin)

async function loadTenants() {
  loading.value = true
  try {
    const res = await getAccessibleTenants()
    tenants.value = (res.data || []) as TenantOption[]
  }
  catch {
    tenants.value = []
  }
  finally {
    loading.value = false
  }
}

function handleChange(val: string | null) {
  emit('update:modelValue', val)
  auth.setCurrentTenantId(val)
}

onMounted(loadTenants)
watch(() => auth.isLoggedIn, (val) => { if (val) loadTenants() })
</script>

<template>
  <el-select
    v-if="isVisible"
    :model-value="modelValue"
    :loading="loading"
    placeholder="选择租户"
    clearable
    style="width: 200px"
    @change="handleChange"
    @visible-change="(v: boolean) => v && loadTenants()"
  >
    <el-option
      v-for="t in tenants"
      :key="t.id"
      :label="t.name"
      :value="t.id"
    />
  </el-select>
</template>
