<script setup lang="ts">
import BackToTop from '@/components/BackToTop.vue'
import { useAppStore } from '@/stores/app'
import AppMain from './AppMain.vue'
import Navbar from './Navbar.vue'
import Sidebar from './Sidebar/index.vue'

const app = useAppStore()
</script>

<template>
  <div class="desktop-shell">
    <Sidebar :is-collapsed="app.sidebarCollapsed" />
    <div class="main-area">
      <Navbar />
      <AppMain />
    </div>
    <BackToTop />
  </div>
</template>

<style scoped lang="scss">
.desktop-shell {
  display: flex;
  height: 100%;
}

.main-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}
</style>
