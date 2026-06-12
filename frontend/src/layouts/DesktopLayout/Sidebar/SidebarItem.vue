<script setup lang="ts">
import type { MenuDto } from '@/types/auth'
import { useRoute } from 'vue-router'
import { resolveIcon } from './icon'

const props = defineProps<{ menu: MenuDto, basePath?: string, isCollapsed?: boolean }>()
const emit = defineEmits<{ expand: [id: string] }>()

const route = useRoute()

const isExpanded = ref(false)
const hasChildren = computed(() => !!(props.menu.children?.length))

function handleClick() {
  if (!hasChildren.value && props.menu.path)
    return
  if (hasChildren.value) {
    isExpanded.value = !isExpanded.value
    emit('expand', props.menu.id)
  }
}

function getChildren(): MenuDto[] {
  return props.menu.children?.filter(c => c.path) ?? []
}

function isChildActive(childPath: string): boolean {
  return route.path === childPath
}
</script>

<template>
  <div v-if="menu.isVisible !== false" class="menu-item-wrapper">
    <!-- 有子菜单 -->
    <div
      v-if="hasChildren"
      class="nav-group__label"
      @click="handleClick"
    >
      <el-icon v-if="menu.icon && resolveIcon(menu.icon)" class="nav-group__icon">
        <component :is="resolveIcon(menu.icon)!" />
      </el-icon>
      <span v-if="!isCollapsed" class="nav-group__text">{{ menu.name }}</span>
      <span v-if="!isCollapsed" class="nav-group__arrow" :class="{ 'nav-group__arrow--open': isExpanded }">▼</span>
    </div>
    <!-- 有子菜单：展开子项 -->
    <div v-if="hasChildren && isExpanded" class="nav-group__children">
      <router-link
        v-for="child in getChildren()"
        :key="child.id"
        :to="child.path ?? '/'"
        class="nav-item nav-item--child"
        :class="{ 'nav-item--active': isChildActive(child.path ?? '') }"
      >
        <span class="nav-item__bar" />
        <el-icon v-if="child.icon && resolveIcon(child.icon)" class="nav-item__icon">
          <component :is="resolveIcon(child.icon)!" />
        </el-icon>
        <span class="nav-item__text">{{ child.name }}</span>
      </router-link>
    </div>
    <!-- 无子菜单：直接跳转 -->
    <router-link
      v-else-if="menu.path"
      :to="menu.path"
      class="nav-item nav-item--top"
      :class="{ 'nav-item--active': route.path === menu.path }"
    >
      <span class="nav-item__bar" />
      <el-icon v-if="menu.icon && resolveIcon(menu.icon)" class="nav-item__icon">
        <component :is="resolveIcon(menu.icon)!" />
      </el-icon>
      <span class="nav-item__text">{{ menu.name }}</span>
    </router-link>
  </div>
</template>

<style scoped lang="scss">
.menu-item-wrapper {
  // container only
}

.nav-group {
  &__label {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
    padding: 10px $spacing-base;
    font-size: $font-size-md;
    font-weight: 500;
    color: $sidebar-text;
    white-space: nowrap;
    cursor: pointer;
    user-select: none;
    border-radius: $radius-md;
    transition:
      color $transition-fast,
      background $transition-fast;

    &:hover {
      color: $sidebar-text-hover;
      background: $sidebar-item-hover;
    }
  }

  &__icon {
    font-size: 16px;
    flex-shrink: 0;
  }
  &__text {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  &__arrow {
    font-size: 8px;
    transition: transform $transition-base;
    flex-shrink: 0;
    opacity: 0.4;
    &--open {
      transform: rotate(-180deg);
      opacity: 0.7;
    }
  }
  &__children {
    overflow: hidden;
    padding-left: $spacing-base;
  }
}

.nav-item {
  position: relative;
  display: flex;
  align-items: center;
  gap: $spacing-sm;
  padding: 10px $spacing-base;
  margin-bottom: 2px;
  font-size: $font-size-md;
  color: $sidebar-text;
  border-radius: $radius-md;
  text-decoration: none;
  white-space: nowrap;
  transition:
    color $transition-fast,
    background $transition-fast,
    padding $transition-fast;

  &__bar {
    position: absolute;
    left: 0;
    top: 50%;
    transform: translateY(-50%);
    width: 3px;
    height: 0;
    background: $color-primary;
    border-radius: 0 2px 2px 0;
    box-shadow: none;
    transition: height $transition-base;
  }

  &__icon {
    font-size: 16px;
    flex-shrink: 0;
  }
  &__text {
    overflow: hidden;
    text-overflow: ellipsis;
  }

  &:hover {
    color: $sidebar-text-hover;
    background: $sidebar-item-hover;
  }

  &--active {
    color: $sidebar-text-active;
    background: $sidebar-item-active;
    .nav-item__bar {
      height: 60%;
    }
  }
}
</style>
