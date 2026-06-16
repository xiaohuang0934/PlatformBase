<script setup lang="ts">
import { IconDatabaseOff, IconLoader2 } from '@tabler/icons-vue'

interface Props {
  /** 空状态图标 */
  icon?: 'empty' | 'loading'
  /** 标题文字 */
  title?: string
  /** 描述文字 */
  description?: string
}

withDefaults(defineProps<Props>(), {
  icon: 'empty',
  title: '暂无数据',
  description: '',
})
</script>

<template>
  <div class="empty-state">
    <div class="empty-state__icon">
      <IconDatabaseOff v-if="icon === 'empty'" class="empty-state__svg" />
      <IconLoader2 v-else class="empty-state__svg empty-state__svg--spin" />
    </div>
    <p class="empty-state__title">
      {{ title }}
    </p>
    <p v-if="description" class="empty-state__description">
      {{ description }}
    </p>
    <div v-if="$slots.default" class="empty-state__actions">
      <slot />
    </div>
  </div>
</template>

<style scoped lang="scss">
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 200px;
  padding: $spacing-xl;
  color: $color-text-dim;

  &__icon {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 64px;
    height: 64px;
    margin-bottom: $spacing-base;
    color: $color-text-placeholder;
    opacity: 0.5;
  }

  &__svg {
    width: 40px;
    height: 40px;

    &--spin {
      animation: spin 1s linear infinite;
    }
  }

  &__title {
    font-size: $font-size-md;
    color: $color-text-secondary;
    margin-bottom: $spacing-xs;
  }

  &__description {
    font-size: $font-size-sm;
    color: $color-text-dim;
    margin-bottom: $spacing-base;
  }

  &__actions {
    margin-top: $spacing-base;
  }
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}
</style>
