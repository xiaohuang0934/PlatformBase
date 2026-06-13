<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useTagsViewStore } from '@/stores/tagsView'

const route = useRoute()
const router = useRouter()
const tagsView = useTagsViewStore()

const visitedViews = computed(() => tagsView.visitedViews)

const contextMenuVisible = ref(false)
const contextMenuPosition = ref({ left: 0, top: 0 })
const selectedTag = ref<any>(null)

/** 判断标签页是否为当前路由 */
function isActive(tag: any): boolean {
  return tag.path === route.path
}

/** 关闭 Tag */
function closeTag(tag: any) {
  const idx = visitedViews.value.findIndex((v: any) => v.path === tag.path)
  if (idx === -1)
    return

  tagsView.delView(tag)

  if (isActive(tag)) {
    const nextIdx = Math.min(idx, visitedViews.value.length - 1)
    const next = visitedViews.value[nextIdx]
    if (next?.path) {
      router.push(next.path)
    }
    else {
      router.push('/')
    }
  }
}

/** 关闭 Others */
function closeOthers(tag: any) {
  tagsView.delOthersViews(tag)
}

/** 关闭 All */
function closeAll() {
  tagsView.delAllViews()
  router.push('/')
}

/** Context Menu */
function handleContextMenu(e: MouseEvent, tag: any) {
  e.preventDefault()
  selectedTag.value = tag
  contextMenuPosition.value = {
    left: e.clientX,
    top: e.clientY,
  }
  contextMenuVisible.value = true
}

/** 关闭 Menu */
function closeMenu() {
  contextMenuVisible.value = false
}

watch(() => route.path, () => {
  if (route.name && route.meta?.title) {
    tagsView.addView({
      name: route.name as string,
      path: route.path,
      fullPath: route.fullPath,
      meta: route.meta,
      query: route.query as Record<string, any>,
    })
  }
  closeMenu()
}, { immediate: true })
</script>

<template>
  <div v-if="visitedViews.length" class="tags-view">
    <div class="tags-view__scroll">
      <router-link
        v-for="tag in visitedViews"
        :key="tag.path"
        :to="tag.path || '/'"
        class="tags-view__item"
        :class="{ 'tags-view__item--active': isActive(tag) }"
        @click.middle.prevent="closeTag(tag)"
        @contextmenu.prevent="handleContextMenu($event, tag)"
      >
        <span class="tags-view__title">{{ tag.title }}</span>
        <button
          v-if="!tag.meta?.affix"
          class="tags-view__close"
          aria-label="关闭"
          @click.prevent.stop="closeTag(tag)"
        >
          ×
        </button>
      </router-link>
    </div>

    <!-- 右键菜单 -->
    <Teleport to="body">
      <div
        v-if="contextMenuVisible"
        class="tags-view__menu"
        :style="{ left: `${contextMenuPosition.left}px`, top: `${contextMenuPosition.top}px` }"
        @click="closeMenu"
      >
        <div class="tags-view__menu-item" @click="closeTag(selectedTag)">
          关闭
        </div>
        <div class="tags-view__menu-item" @click="closeOthers(selectedTag)">
          关闭其他
        </div>
        <div class="tags-view__menu-item" @click="closeAll">
          关闭所有
        </div>
      </div>
    </Teleport>
  </div>

  <!-- 点击空白区域关闭右键菜单 -->
  <div v-if="contextMenuVisible" class="tags-view__mask" @click="closeMenu" />
</template>

<style scoped lang="scss">
.tags-view {
  display: flex;
  align-items: center;
  height: 34px;
  padding: 0 $spacing-sm;
  background: $color-bg-base;
  border-bottom: 1px solid $header-border;

  &__scroll {
    display: flex;
    gap: 4px;
    overflow-x: auto;
    white-space: nowrap;
    scrollbar-width: none;
    &::-webkit-scrollbar {
      display: none;
    }
  }

  &__item {
    position: relative;
    display: flex;
    align-items: center;
    gap: 4px;
    height: 26px;
    padding: 0 10px;
    font-size: $font-size-sm;
    color: $color-text-secondary;
    background: $color-bg-card;
    border: 1px solid $color-border-light;
    border-radius: $radius-sm;
    text-decoration: none;
    cursor: pointer;
    user-select: none;
    transition: all $transition-fast;

    &:hover {
      color: $color-text-primary;
      background: $color-bg-elevated;
      border-color: $color-border;
    }

    &--active {
      color: $color-primary;
      background: $color-primary-dim;
      border-color: $color-border-accent;
    }
  }

  &__title {
    max-width: 120px;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  &__close {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 14px;
    height: 14px;
    padding: 0;
    font-size: 10px;
    line-height: 1;
    color: inherit;
    background: none;
    border: none;
    border-radius: 50%;
    cursor: pointer;
    transition: background $transition-fast;

    &:hover {
      color: #fff;
      background: $color-danger;
    }
  }

  &__menu {
    position: fixed;
    z-index: 9999;
    min-width: 100px;
    padding: $spacing-xs 0;
    background: $color-bg-elevated;
    border: 1px solid $color-border;
    border-radius: $radius-md;
    box-shadow: $shadow-lg;
  }

  &__menu-item {
    padding: $spacing-sm $spacing-lg;
    font-size: $font-size-sm;
    color: $color-text-regular;
    cursor: pointer;

    &:hover {
      background: $color-bg-hover;
      color: $color-primary;
    }
  }

  &__mask {
    position: fixed;
    inset: 0;
    z-index: 9998;
  }
}
</style>
