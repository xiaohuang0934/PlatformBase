<script setup lang="ts">
import { usePermissionStore } from '@/stores/permission'
import Logo from './Logo.vue'
import SidebarItem from './SidebarItem.vue'

defineProps<{ isCollapsed: boolean }>()

const permission = usePermissionStore()
</script>

<template>
  <aside class="sidebar" :class="{ 'sidebar--collapsed': isCollapsed }">
    <Logo />
    <nav class="sidebar-nav">
      <SidebarItem
        v-for="menu in permission.menuTree"
        :key="menu.id"
        :menu="menu"
        :is-collapsed="isCollapsed"
      />
    </nav>
  </aside>
</template>

<style scoped lang="scss">
.sidebar {
  display: flex;
  flex-direction: column;
  width: $sidebar-width;
  background: $sidebar-bg;
  border-right: 1px solid $sidebar-border;
  transition: width $transition-base;
  flex-shrink: 0;
  overflow: hidden;

  &--collapsed {
    width: $sidebar-collapsed-width;
  }
}

.sidebar-nav {
  flex: 1;
  padding: $spacing-sm $spacing-sm;
  overflow-y: auto;
}
</style>
