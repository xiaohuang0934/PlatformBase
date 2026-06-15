<script setup lang="ts">
defineProps<{
  modelValue: boolean
  title: string
  width?: string
  submitting?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: []
  closed: []
}>()

function handleClose() {
  emit('update:modelValue', false)
}

function handleClosed() {
  emit('closed')
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="title"
    :width="width || '480px'"
    destroy-on-close
    @update:model-value="emit('update:modelValue', $event)"
    @closed="handleClosed"
  >
    <slot />
    <template #footer>
      <el-button @click="handleClose">
        取消
      </el-button>
      <el-button type="primary" :loading="submitting" @click="emit('confirm')">
        确定
      </el-button>
    </template>
  </el-dialog>
</template>
