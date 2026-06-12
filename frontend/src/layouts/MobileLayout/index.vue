<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

const activeTab = ref(0)

const tabItems = [
  { name: '工作台', path: '/dashboard', icon: 'home-o' },
  { name: '应用', path: '/m/apps', icon: 'apps-o' },
  { name: '消息', path: '/m/notifications', icon: 'chat-o' },
  { name: '我的', path: '/m/profile', icon: 'user-o' },
]

function onTabChange(index: number) {
  activeTab.value = index
  const item = tabItems[index]
  if (item?.path) {
    router.push(item.path)
  }
}
</script>

<template>
  <div class="mobile-shell">
    <!-- 内容区 -->
    <div class="mobile-shell__content">
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
  }
}

// --- 底部导航栏 ---
.mobile-tabbar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 100;
  display: flex;
  height: $tabbar-height;
  padding-bottom: env(safe-area-inset-bottom);
  background: rgba(14, 14, 31, 0.92);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border-top: 1px solid rgba(255, 255, 255, 0.06);
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
    text-shadow: 0 0 12px rgba(0, 229, 255, 0.2);
  }
}
</style>
