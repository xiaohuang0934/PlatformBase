<script setup lang="ts">
import { IconBolt, IconLock, IconShield } from '@tabler/icons-vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h2 class="page-header__title">
          工作台
        </h2>
        <p class="page-header__subtitle">
          欢迎回来，{{ auth.displayName }}
        </p>
      </div>
    </div>

    <!-- 统计卡片 -->
    <div class="card-grid">
      <div class="stat-card">
        <div class="stat-card__icon">
          <IconBolt class="stat-card__icon-svg" />
        </div>
        <div class="stat-card__body">
          <p class="stat-card__value">
            {{ auth.user?.username }}
          </p>
          <p class="stat-card__label">
            当前用户
          </p>
        </div>
      </div>

      <div class="stat-card">
        <div class="stat-card__icon">
          <IconLock class="stat-card__icon-svg" />
        </div>
        <div class="stat-card__body">
          <p class="stat-card__value">
            {{ auth.roles.length }}
          </p>
          <p class="stat-card__label">
            角色数量
          </p>
        </div>
      </div>

      <div class="stat-card">
        <div class="stat-card__icon">
          <IconShield class="stat-card__icon-svg" />
        </div>
        <div class="stat-card__body">
          <p class="stat-card__value">
            {{ auth.permissionCodes.length }}
          </p>
          <p class="stat-card__label">
            权限数量
          </p>
        </div>
      </div>
    </div>

    <!-- 占位 -->
    <div class="page-container" style="margin-top: 24px; text-align: center">
      <p class="coming-soon">
        更多功能模块开发中
      </p>
    </div>
  </div>
</template>

<style scoped lang="scss">
.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: $spacing-lg;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: $spacing-md;
  padding: $spacing-lg;
  background: $card-bg-alpha;
  border: 1px solid $card-border-alpha;
  border-radius: $radius-lg;
  transition: all $transition-base;

  &:hover {
    border-color: $card-hover-border;
    transform: translateY(-1px);
    box-shadow: $shadow-base;
  }

  &__icon {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 48px;
    height: 48px;
    background: $color-primary-dim;
    border-radius: $radius-md;
    flex-shrink: 0;
  }

  &__icon-svg {
    width: 24px;
    height: 24px;
    color: $color-primary;
  }

  &__body {
    flex: 1;
  }

  &__value {
    font-family: $font-display;
    font-size: $font-size-2xl;
    font-weight: 600;
    color: $color-text-primary;
    line-height: 1.2;
  }

  &__label {
    font-size: $font-size-sm;
    color: $color-text-dim;
    margin-top: 2px;
  }
}

.coming-soon {
  color: $color-text-dim;
  font-size: $font-size-md;
  padding: 48px 0;
}
</style>
