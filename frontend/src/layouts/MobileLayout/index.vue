<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { usePermissionStore } from '@/stores/permission'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const permission = usePermissionStore()

const drawerVisible = ref(false)

const tabItems = [
  { name: '工作台', path: '/dashboard', icon: 'home-o' },
  { name: '应用', path: '/m/apps', icon: 'apps-o' },
  { name: '消息', path: '/m/notifications', icon: 'chat-o' },
  { name: '我的', path: '/m/profile', icon: 'user-o' },
]

const activeTab = ref(0)

function onTabChange(index: number) {
  activeTab.value = index
  const item = tabItems[index]
  if (item?.path)
    router.push(item.path)
}

function goTo(path: string) {
  drawerVisible.value = false
  router.push(path)
}

function handleLogout() {
  drawerVisible.value = false
  auth.logoutAction()
  permission.reset()
  router.push('/login')
}

function goBack() {
  if (window.history.length > 1)
    router.back()
  else router.push('/dashboard')
}

/** 当前在模块内部（非 tab 首页），显示返回按钮 */
const showBack = computed(() => {
  const tabPaths = tabItems.map(t => t.path)
  return !tabPaths.includes(route.path) && route.path !== '/'
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
              <div class="drawer-group__label">
                {{ menu.name }}
              </div>
              <div
                v-for="child in menu.children"
                :key="child.id"
                class="drawer-item"
                @click="goTo(child.path ?? '/')"
              >
                {{ child.name }}
              </div>
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
  margin-bottom: $spacing-sm;
  &__label {
    padding: $spacing-sm $spacing-md;
    font-size: $font-size-xs;
    font-weight: 600;
    color: $color-text-dim;
    text-transform: uppercase;
  }
}

.drawer-item {
  padding: 12px $spacing-md;
  font-size: $font-size-base;
  color: $color-text-regular;
  cursor: pointer;
  &:active {
    background: $color-bg-hover;
    color: $color-primary;
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
}
</style>
