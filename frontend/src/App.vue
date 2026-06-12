<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useDevice } from '@/composables/useDevice'
import DesktopLayout from '@/layouts/DesktopLayout/index.vue'
import MobileLayout from '@/layouts/MobileLayout/index.vue'

const route = useRoute()
const { isMobile } = useDevice()

const isStandalone = computed(() => route.meta?.noAuth === true)
const useMobile = computed(() => isMobile.value && !isStandalone.value)
</script>

<template>
  <router-view v-if="isStandalone" />
  <MobileLayout v-else-if="useMobile" />
  <DesktopLayout v-else />
</template>
