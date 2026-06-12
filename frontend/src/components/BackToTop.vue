<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { scrollTo } from '@/utils/scroll-to'

const visible = ref(false)

function handleScroll() {
  visible.value = window.scrollY > 300
}

function scrollToTop() {
  scrollTo(0, 300)
}

onMounted(() => window.addEventListener('scroll', handleScroll))
onUnmounted(() => window.removeEventListener('scroll', handleScroll))
</script>

<template>
  <Transition name="fade">
    <div v-if="visible" class="back-to-top" @click="scrollToTop">
      <span class="back-to-top__icon">↑</span>
    </div>
  </Transition>
</template>

<style scoped lang="scss">
.back-to-top {
  position: fixed;
  right: 24px;
  bottom: 48px;
  z-index: 50;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  background: $color-bg-elevated;
  border: 1px solid $color-border;
  border-radius: 50%;
  cursor: pointer;
  box-shadow: $shadow-base;
  transition: all $transition-fast;

  &:hover {
    background: $color-primary-dim;
    border-color: $color-border-accent;
  }

  &__icon {
    font-size: 18px;
    color: $color-text-secondary;
  }
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity $transition-base;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
