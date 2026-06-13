<script setup lang="ts">
import type { MenuDto } from '@/types/auth'
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import * as menuApi from '@/api/menus'

const router = useRouter()
const loading = ref(false)
const menuTree = ref<MenuDto[]>([])

/** 获取 List */
async function fetchList() {
  loading.value = true
  try {
    const res = await menuApi.getMenuList()
    // 前端过滤：只展示一级菜单（parentId 为空）
    menuTree.value = (res.data ?? []).filter(m => !m.parentId)
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** 进入子菜单页面 */
function goChildren(item: MenuDto) { router.push(`/m/menus/${item.id}/children?title=${encodeURIComponent(item.name)}`) }
/** 跳转到编辑页 */
function goEdit(id?: string) { router.push(id ? `/m/menus/${id}/edit` : '/m/menus/create') }

onMounted(fetchList)
</script>

<template>
  <div class="m-page">
    <div class="m-toolbar">
      <van-button type="primary" block round @click="goEdit()">
        添加菜单
      </van-button>
    </div>

    <div v-if="!loading && !menuTree.length" class="m-empty">
      <span class="m-empty__text">暂无菜单数据</span>
    </div>

    <van-cell-group inset>
      <van-cell v-for="item in menuTree" :key="item.id" :title="item.name" is-link @click="goChildren(item)">
        <template #icon>
          <span style="font-size:16px;margin-right:6px">📁</span>
        </template>
      </van-cell>
    </van-cell-group>
  </div>
</template>

<style scoped lang="scss">
.m-toolbar {
  padding: 8px 12px;
}
</style>
