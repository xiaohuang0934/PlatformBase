<script setup lang="ts">
import type { MenuDto } from '@/types/auth'
import { useRoute, useRouter } from 'vue-router'
import { usePermissionStore } from '@/stores/permission'

const route = useRoute()
const router = useRouter()
const permission = usePermissionStore()

interface BreadcrumbItem {
  name: string
  path: string
}

const breadcrumbs = computed<BreadcrumbItem[]>(() => {
  const items: BreadcrumbItem[] = []
  const currentPath = route.path

  // 从菜单树中查找路径层级
  function findPath(menus: MenuDto[], targetPath: string, parents: MenuDto[] = []): MenuDto[] | null {
    for (const menu of menus) {
      const chain = [...parents, menu]
      if (menu.path === targetPath)
        return chain
      if (menu.children?.length) {
        const found = findPath(menu.children, targetPath, chain)
        if (found)
          return found
      }
    }
    return null
  }

  const chain = findPath(permission.menuTree, currentPath)
  if (chain) {
    for (const menu of chain) {
      items.push({ name: menu.name, path: menu.path || '' })
    }
  }
  else {
    items.push({ name: (route.meta?.title as string) || '', path: currentPath })
  }

  return items
})

function handleClick(item: BreadcrumbItem) {
  if (item.path && item.path !== route.path) {
    router.push(item.path)
  }
}
</script>

<template>
  <el-breadcrumb
    v-if="breadcrumbs.length > 1"
    separator="/"
    class="breadcrumb-container"
  >
    <el-breadcrumb-item
      v-for="(item, index) in breadcrumbs"
      :key="item.path || index"
    >
      <span
        :style="{
          cursor: index < breadcrumbs.length - 1 ? 'pointer' : 'default',
          color: index < breadcrumbs.length - 1 ? 'var(--color-text-secondary)' : 'var(--color-text-primary)',
        }"
        @click="handleClick(item)"
      >
        {{ item.name }}
      </span>
    </el-breadcrumb-item>
  </el-breadcrumb>
</template>

<style scoped lang="scss">
.breadcrumb-container {
  font-size: $font-size-base;
  line-height: 1.5;
}
</style>
