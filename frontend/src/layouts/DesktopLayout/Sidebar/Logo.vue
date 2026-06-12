<script setup lang="ts">
import { useSettingsStore } from '@/stores/settings'

const settings = useSettingsStore()
</script>

<template>
  <router-link v-if="settings.sidebarLogo" to="/" class="sidebar-logo">
    <span class="sidebar-logo__text">PlatformBase</span>
  </router-link>
</template>

<style scoped lang="scss">
.sidebar-logo {
  display: flex;
  align-items: center;
  justify-content: center;
  height: $header-height;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  flex-shrink: 0;
  text-decoration: none;

  &__text {
    font-family: $font-display;
    font-size: $font-size-xl;
    font-weight: 600;
    letter-spacing: 0.04em;
    color: $color-text-primary;
  }
}
</style>
