<script setup lang="ts">
import { useRouter } from 'vue-router'
import { resolveIcon } from '@/layouts/DesktopLayout/Sidebar/icon'
import { usePermissionStore } from '@/stores/permission'

const router = useRouter()
const permission = usePermissionStore()

const appItems = computed(() => {
  const items: { name: string, icon: string, path: string }[] = []
  for (const menu of permission.menuTree) {
    if (menu.children?.length) {
      for (const child of menu.children) {
        if (child.path) {
          items.push({ name: child.name, icon: child.icon || 'IconApps', path: child.path })
        }
      }
    }
  }
  return items
})

/** 打开 App */
function openApp(item: { path: string }) { router.push(item.path) }
</script>

<template>
  <div class="m-page">
    <div class="app-grid">
      <div
        v-for="item in appItems"
        :key="item.path"
        class="app-item"
        @click="openApp(item)"
      >
        <div class="app-item__icon">
          <el-icon :size="24">
            <component :is="resolveIcon(item.icon)" />
          </el-icon>
        </div>
        <span class="app-item__name">{{ item.name }}</span>
      </div>
    </div>

    <div v-if="!appItems.length" class="m-empty">
      <span class="m-empty__icon">📋</span>
      <span class="m-empty__text">暂无可用应用</span>
    </div>
  </div>
</template>

<style scoped lang="scss">
.app-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: $spacing-lg;
  padding: $spacing-lg;
}

.app-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: $spacing-sm;
  cursor: pointer;

  &__icon {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 52px;
    height: 52px;
    background: $color-primary-dim;
    border-radius: $radius-md;
    color: $color-primary;
  }

  &__name {
    font-size: $font-size-sm;
    color: $color-text-regular;
    text-align: center;
  }
}
</style>
