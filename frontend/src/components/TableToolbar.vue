<script setup lang="ts">
defineProps<{ selectedCount?: number }>()
</script>

<template>
  <div class="table-toolbar">
    <div class="table-toolbar__left">
      <slot name="actions" />
      <span v-if="selectedCount && selectedCount > 0" class="table-toolbar__count">
        已选 {{ selectedCount }} 项
      </span>
    </div>
    <div class="table-toolbar__right">
      <slot name="extra" />
    </div>
  </div>
</template>

<style scoped lang="scss">
.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: $spacing-base;

  &__left {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
  }

  &__right {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
  }

  &__count {
    font-size: $font-size-sm;
    color: $color-text-secondary;
    padding-left: $spacing-sm;
    border-left: 1px solid $color-border;
  }
}
</style>
