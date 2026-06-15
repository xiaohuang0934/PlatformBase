<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { usePermissionStore } from '@/stores/permission'
import { useThemeStore } from '@/stores/theme'
import { UserType } from '@/types/user'

const router = useRouter()
const auth = useAuthStore()
const permission = usePermissionStore()
const theme = useThemeStore()

const userTypeLabel: Record<number, string> = {
  [UserType.PlatformAdmin]: '平台管理员',
  [UserType.TenantAdmin]: '租户管理员',
  [UserType.TenantUser]: '租户用户',
}

const roleLabel = computed(() => {
  if (!auth.user) return ''
  return userTypeLabel[auth.user.userType] || `用户类型${auth.user.userType}`
})

function handleLogout() {
  auth.logoutAction()
  permission.reset()
  router.push('/login')
}
</script>

<template>
  <div class="m-page">
    <div class="m-card-list">
      <div class="m-card-list__item profile-card">
        <div class="profile-card__avatar">
          {{ (auth.user?.username || '?')[0].toUpperCase() }}
        </div>
        <div class="profile-card__info">
          <span class="profile-card__name">{{ auth.displayName }}</span>
          <span class="profile-card__role">
            {{ roleLabel }}
          </span>
        </div>
      </div>

      <div class="m-card-list__item">
        <div class="info-row" @click="theme.toggle()">
          <span class="info-row__label">主题模式</span>
          <span class="info-row__value">{{ theme.mode === 'dark' ? '暗色' : '亮色' }}</span>
        </div>
      </div>
    </div>

    <div class="m-bottom-action" style="position:static; padding-top:24px">
      <van-button type="danger" block @click="handleLogout">
        退出登录
      </van-button>
    </div>
  </div>
</template>

<style scoped lang="scss">
.profile-card {
  display: flex;
  align-items: center;
  gap: $spacing-md;

  &__avatar {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 48px;
    height: 48px;
    font-size: $font-size-lg;
    font-weight: 600;
    color: $color-primary;
    background: $color-primary-dim;
    border-radius: 50%;
  }

  &__info {
    display: flex;
    flex-direction: column;
  }

  &__name {
    font-size: $font-size-md;
    font-weight: 600;
    color: $color-text-primary;
  }

  &__role {
    font-size: $font-size-sm;
    color: $color-text-dim;
    margin-top: 2px;
  }
}

.info-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  cursor: pointer;

  &__label {
    font-size: $font-size-md;
    color: $color-text-regular;
  }

  &__value {
    font-size: $font-size-sm;
    color: $color-text-dim;
  }
}
</style>
