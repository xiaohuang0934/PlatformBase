<script setup lang="ts">
import TagsView from '@/components/TagsView.vue'
import { useSettingsStore } from '@/stores/settings'
import { useTagsViewStore } from '@/stores/tagsView'

const settings = useSettingsStore()
const tagsView = useTagsViewStore()
</script>

<template>
  <section class="app-main">
    <TagsView v-if="settings.showTagsView && tagsView.visitedViews.length" />
    <div class="app-main__content">
      <router-view v-slot="{ Component, route: r }">
        <keep-alive :include="tagsView.cachedViewNames">
          <component :is="Component" :key="r.path" />
        </keep-alive>
      </router-view>
    </div>
  </section>
</template>

<style scoped lang="scss">
.app-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;

  &__content {
    flex: 1;
    padding: $spacing-lg;
    overflow-y: auto;
    background: $color-bg-deep;
  }
}
</style>
