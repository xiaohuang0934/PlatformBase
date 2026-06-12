<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <div class="m-page">
    <div class="m-page-header">
      <span class="m-page-header__title">工作台</span>
    </div>

    <div class="m-card-list">
      <div class="m-card-list__item">
        <div class="welcome-row">
          <span class="welcome-row__label">欢迎回来</span>
          <span class="welcome-row__value">
            {{ auth.displayName }}
          </span>
        </div>
      </div>

      <div class="m-card-list__item">
        <div class="stat-row">
          <span class="stat-row__label">角色</span>
          <span class="stat-row__value">{{ auth.roles.length }}</span>
        </div>
      </div>

      <div class="m-card-list__item">
        <div class="stat-row">
          <span class="stat-row__label">权限</span>
          <span class="stat-row__value">{{ auth.permissionCodes.length }}</span>
        </div>
      </div>
    </div>

    <div class="m-empty">
      <span class="m-empty__icon">● ● ●</span>
      <span class="m-empty__text">更多功能开发中</span>
    </div>
  </div>
</template>

<style scoped lang="scss">
.welcome-row {
  display: flex;
  justify-content: space-between;
  align-items: center;

  &__label {
    font-size: $font-size-md;
    color: $color-text-secondary;
  }

  &__value {
    font-family: $font-display;
    font-size: $font-size-lg;
    font-weight: 600;
  }
}

.stat-row {
  display: flex;
  justify-content: space-between;
  align-items: center;

  &__label {
    font-size: $font-size-md;
    color: $color-text-secondary;
  }

  &__value {
    font-size: $font-size-lg;
    font-weight: 700;
    color: $color-text-primary;
  }
}
</style>
