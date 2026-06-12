<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()

const { params, query } = route
const { path } = params

router.replace({ path: `/${path || ''}`, query })
</script>

<template>
  <div class="redirect-page" />
</template>

<style scoped lang="scss">
.redirect-page {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
