<script setup lang="ts">
import type { Component } from 'vue'
import { computed } from 'vue'
import { resolveIcon } from '@/layouts/DesktopLayout/Sidebar/icon'

const props = defineProps<{
  icon: string | null
  name: string
  path: string
}>()

const emit = defineEmits<{
  click: [path: string]
}>()

const iconComponent = computed<Component | null>(() => resolveIcon(props.icon))

/** 转换为移动端路径 */
const mobilePath = computed(() => {
  return props.path.startsWith('/m/') ? props.path : `/m${props.path}`
})

function handleClick() {
  emit('click', mobilePath.value)
}
</script>

<template>
  <div class="drawer-item" @click="handleClick">
    <el-icon v-if="iconComponent" class="drawer-item__icon">
      <component :is="iconComponent" />
    </el-icon>
    <span class="drawer-item__text">{{ name }}</span>
  </div>
</template>

<style scoped lang="scss">
.drawer-item {
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  padding: 12px $spacing-md;
  font-size: $font-size-base;
  color: $color-text-regular;
  cursor: pointer;
  transition: all $transition-fast;

  &:active {
    background: $color-bg-hover;
    color: $color-primary;
  }

  &__icon {
    width: 18px;
    height: 18px;
    flex-shrink: 0;
    color: $color-text-dim;
  }

  &__text {
    flex: 1;
  }
}
</style>
