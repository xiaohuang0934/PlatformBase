<script setup lang="ts">
import type { Component } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { resolveIcon } from '@/layouts/DesktopLayout/Sidebar/icon'
import { useAuthStore } from '@/stores/auth'
import { usePermissionStore } from '@/stores/permission'
import DrawerMenuItem from './DrawerMenuItem.vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const permission = usePermissionStore()

const drawerVisible = ref(false)

const tabItems = [
  { name: '工作台', path: '/m/dashboard', icon: 'home-o' },
  { name: '应用', path: '/m/apps', icon: 'apps-o' },
  { name: '消息', path: '/m/notifications', icon: 'chat-o' },
  { name: '我的', path: '/m/profile', icon: 'user-o' },
]

const activeTab = ref(0)

/** TabBar切换事件 */
function onTabChange(index: number) {
  activeTab.value = index
  const item = tabItems[index]
  if (item?.path)
    router.push(item.path)
}

/** 抽屉导航跳转 — 接收已转换的移动端路径 */
function goTo(path: string) {
  drawerVisible.value = false
  router.push(path)
}

/** Logout */
function handleLogout() {
  drawerVisible.value = false
  auth.logoutAction()
  permission.reset()
  router.push('/login')
}

/** 返回上一页 */
function goBack() {
  if (window.history.length > 1)
    router.back()
  else router.push('/m/dashboard')
}

/** 当前在模块内部（非 tab 首页），显示返回按钮 */
const showBack = computed(() => {
  const tabPaths = tabItems.map(t => t.path)
  return !tabPaths.includes(route.path) && route.path !== '/' && route.path !== '/login'
})

const navItems = computed(() => {
  return permission.menuTree
    .filter(m => m.isVisible !== false)
    .map(m => ({
      name: m.name,
      icon: m.icon || 'point-gift-o',
      children: m.children?.filter(c => c.path) ?? [],
    }))
})

/** 解析菜单图标 */
function resolveMenuIcon(name: string | null): Component | null {
  return resolveIcon(name)
}
</script>

<template>
  <div class="mobile-shell">
    <!-- 全局面包屑导航（模块内部） -->
    <van-nav-bar
      v-if="showBack"
      :title="(route.meta?.title as string) || ''"
      left-arrow
      fixed
      placeholder
      @click-left="goBack"
    />

    <!-- 内容区 -->
    <div class="mobile-shell__content" :class="{ 'mobile-shell__content--nav': showBack }">
      <router-view />
    </div>

    <!-- 底部 TabBar -->
    <div class="mobile-tabbar">
      <button
        v-for="(item, index) in tabItems"
        :key="index"
        class="tabbar-item"
        :class="{ 'tabbar-item--active': activeTab === index }"
        @click="onTabChange(index)"
      >
        <van-icon :name="item.icon" size="22" />
        <span class="tabbar-item__label">{{ item.name }}</span>
      </button>
    </div>

    <!-- 抽屉导航 -->
    <van-popup v-model:show="drawerVisible" position="left" :style="{ width: '75%', height: '100%' }">
      <div class="mobile-drawer">
        <div class="mobile-drawer__header">
          <span class="mobile-drawer__title">功能菜单</span>
        </div>
        <div class="mobile-drawer__list">
          <template v-for="menu in navItems" :key="menu.name">
            <div class="drawer-group">
              <div class="drawer-group__header">
                <el-icon v-if="resolveMenuIcon(menu.icon)" class="drawer-group__icon">
                  <component :is="resolveMenuIcon(menu.icon)" />
                </el-icon>
                <span class="drawer-group__label">{{ menu.name }}</span>
              </div>
              <div class="drawer-group__divider" />
              <DrawerMenuItem
                v-for="child in menu.children"
                :key="child.id"
                :icon="child.icon"
                :name="child.name"
                :path="child.path ?? '/'"
                @click="goTo"
              />
            </div>
          </template>
        </div>
        <div class="mobile-drawer__footer">
          <button class="drawer-logout" @click="handleLogout">
            退出登录
          </button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped lang="scss">
.mobile-shell {
  height: 100%;
  display: flex;
  flex-direction: column;

  &__content {
    flex: 1;
    overflow-y: auto;
    padding-bottom: calc($tabbar-height + env(safe-area-inset-bottom));
    -webkit-overflow-scrolling: touch;

    &--nav {
      padding-top: 0;
    }
  }
}

.mobile-tabbar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 100;
  display: flex;
  height: $tabbar-height;
  padding-bottom: env(safe-area-inset-bottom);
  background: $tabbar-bg;
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border-top: 1px solid $header-border;
}

.tabbar-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  background: none;
  border: none;
  color: $color-text-dim;
  cursor: pointer;
  transition: color $transition-fast;
  -webkit-tap-highlight-color: transparent;

  &__label {
    font-size: $font-size-xs;
    font-weight: 500;
  }
  &:active {
    opacity: 0.7;
  }
  &--active {
    color: $color-primary;
  }
}

// --- 抽屉 ---
.mobile-drawer {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: $color-bg-base;

  &__header {
    display: flex;
    align-items: center;
    height: 48px;
    padding: 0 $spacing-md;
    border-bottom: 1px solid $header-border;
  }
  &__title {
    font-size: $font-size-md;
    font-weight: 600;
    color: $color-text-primary;
  }
  &__list {
    flex: 1;
    overflow-y: auto;
    padding: $spacing-sm 0;
  }
  &__footer {
    padding: $spacing-md;
    border-top: 1px solid $header-border;
  }
}

.drawer-group {
  margin-bottom: $spacing-base;
  &__header {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
    padding: $spacing-sm $spacing-md;
  }
  &__icon {
    width: 16px;
    height: 16px;
    color: $color-text-dim;
  }
  &__label {
    font-size: $font-size-xs;
    font-weight: 600;
    color: $color-text-dim;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }
  &__divider {
    height: 1px;
    margin: 0 $spacing-md;
    background: $header-border;
  }
}

.drawer-logout {
  width: 100%;
  padding: 10px;
  font-size: $font-size-base;
  color: $color-danger;
  background: none;
  border: 1px solid $color-border;
  border-radius: $radius-md;
  cursor: pointer;
  transition: all $transition-fast;

  &:active {
    background: rgba(229, 72, 77, 0.08);
  }
}
</style>
