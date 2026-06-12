<script setup lang="ts">
defineProps<{ isActive: boolean }>()
const emit = defineEmits<{ toggle: [] }>()
</script>

<template>
  <button
    class="hamburger"
    :class="{ 'is-active': isActive }"
    aria-label="展开或收起侧边菜单"
    @click="emit('toggle')"
  >
    <span class="hamburger__line" />
    <span class="hamburger__line" />
    <span class="hamburger__line" />
  </button>
</template>

<style scoped lang="scss">
.hamburger {
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 4px;
  width: 20px;
  height: 20px;
  cursor: pointer;
  padding: 0;
  background: transparent;
  border: none;

  &__line {
    display: block;
    width: 100%;
    height: 2px;
    background: $color-text-secondary;
    border-radius: 1px;
    transition:
      transform $transition-fast,
      opacity $transition-fast;
    transform-origin: center;
  }

  &.is-active &__line {
    &:first-child {
      transform: translateY(6px) rotate(45deg);
    }
    &:nth-child(2) {
      opacity: 0;
    }
    &:last-child {
      transform: translateY(-6px) rotate(-45deg);
    }
  }

  &:hover &__line {
    background: $color-text-primary;
  }
}
</style>
