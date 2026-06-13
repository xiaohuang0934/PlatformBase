<script setup lang="ts">
import { ElMessageBox } from 'element-plus'
import { ref } from 'vue'
import Breadcrumb from '@/components/Breadcrumb.vue'
import ChangePasswordDialog from '@/components/ChangePasswordDialog.vue'
import Hamburger from '@/components/Hamburger.vue'
import { useAppStore } from '@/stores/app'
import { useAuthStore } from '@/stores/auth'
import { usePermissionStore } from '@/stores/permission'
import { useSettingsStore } from '@/stores/settings'
import { useThemeStore } from '@/stores/theme'

const router = useRouter()
const auth = useAuthStore()
const app = useAppStore()
const permission = usePermissionStore()
const theme = useThemeStore()
const settings = useSettingsStore()
const showPwdDialog = ref(false)

async function handleLogout() {
  try {
    await ElMessageBox.confirm('确定要退出登录吗？', '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning',
    })
  }
  catch { return }
  await auth.logoutAction()
  permission.reset()
  router.push('/login')
}
</script>

<template>
  <div class="navbar" :class="{ 'navbar--fixed': settings.fixedHeader }">
    <div class="navbar__left">
      <Hamburger :is-active="app.sidebarCollapsed" @toggle="app.toggleSidebar()" />
      <Breadcrumb class="navbar__breadcrumb" />
    </div>

    <div class="navbar__right">
      <button class="navbar__icon-btn" @click="theme.toggle()">
        {{ theme.mode === 'dark' ? '☀️' : '🌙' }}
      </button>

      <div class="user-menu" tabindex="0">
        <span class="user-menu__name">{{ auth.displayName }}</span>
        <div class="user-menu__avatar">
          {{ (auth.user?.username || '?')[0] }}
        </div>
        <div class="user-menu__dropdown">
          <button class="user-menu__item" @click="showPwdDialog = true">
            修改密码
          </button>
          <button class="user-menu__item" @click="handleLogout">
            退出登录
          </button>
        </div>
      </div>
    </div>
  </div>
  <ChangePasswordDialog v-model:visible="showPwdDialog" />
</template>

<style scoped lang="scss">
.navbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: $header-height;
  padding: 0 $spacing-lg;
  background: $header-bg;
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border-bottom: 1px solid $header-border;
  flex-shrink: 0;
  z-index: 5;

  &--fixed {
    position: sticky;
    top: 0;
  }

  &__left {
    display: flex;
    align-items: center;
    gap: $spacing-md;
  }

  &__breadcrumb {
    margin-left: $spacing-sm;
  }

  &__right {
    display: flex;
    align-items: center;
    gap: $spacing-md;
  }

  &__icon-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 36px;
    height: 36px;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.06);
    border-radius: $radius-base;
    cursor: pointer;
    font-size: 16px;
    transition: all $transition-fast;

    &:hover {
      background: rgba(255, 255, 255, 0.06);
      border-color: rgba(255, 255, 255, 0.1);
    }
  }
}

.user-menu {
  position: relative;
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  cursor: pointer;

  &__name {
    font-size: $font-size-md;
    color: $color-text-regular;
  }

  &__avatar {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    font-size: $font-size-sm;
    font-weight: 600;
    color: $color-primary;
    background: $color-primary-dim;
    border: 1px solid $color-border-accent;
    border-radius: 50%;
    transition: border-color $transition-fast;
    &:hover {
      border-color: $color-primary;
    }
  }

  &__dropdown {
    position: absolute;
    top: calc(100% + 8px);
    right: 0;
    width: 140px;
    padding: $spacing-xs;
    background: $color-bg-elevated;
    border: 1px solid rgba(255, 255, 255, 0.08);
    border-radius: $radius-md;
    box-shadow: $shadow-lg;
    opacity: 0;
    visibility: hidden;
    transform: translateY(-4px);
    transition: all $transition-fast;
    z-index: 20;
  }

  &:hover &__dropdown,
  &:focus-within &__dropdown {
    opacity: 1;
    visibility: visible;
    transform: translateY(0);
  }

  &__item {
    width: 100%;
    padding: $spacing-sm $spacing-base;
    background: none;
    border: none;
    border-radius: $radius-sm;
    font-size: $font-size-md;
    color: $color-text-regular;
    cursor: pointer;
    text-align: left;
    transition: all $transition-fast;
    &:hover {
      color: $color-danger;
      background: rgba(248, 113, 113, 0.08);
    }
  }
}
</style>
