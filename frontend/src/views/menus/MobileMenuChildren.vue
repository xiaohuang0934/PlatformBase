<script setup lang="ts">
import type { MenuDto } from '@/types/auth'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as menuApi from '@/api/menus'

const route = useRoute()
const router = useRouter()
const parentId = route.params.parentId as string
const parentName = ref('')
const loading = ref(false)
const children = ref<MenuDto[]>([])

/** 获取 List */
async function fetchList() {
  loading.value = true
  try {
    const res = await menuApi.getMenuList(parentId)
    parentName.value = (route.query.title as string) || ''
    children.value = res.data ?? []
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** 跳转到编辑页 */
function goEdit(childId?: string) {
  if (childId)
    router.push(`/m/menus/${childId}/edit`)
  else router.push(`/m/menus/create?parentId=${parentId}`)
}

/** Delete */
async function handleDelete(row: MenuDto) {
  try { await ElMessageBox.confirm(`确定删除 "${row.name}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  try { await menuApi.deleteMenu(row.id); ElMessage.success('已删除'); fetchList() }
  catch { ElMessage.error('删除失败') }
}

onMounted(fetchList)
</script>

<template>
  <div class="m-page">
    <van-nav-bar :title="parentName || '子菜单'" left-arrow fixed placeholder @click-left="router.back()" />

    <div class="m-toolbar">
      <van-button type="primary" block round @click="goEdit()">
        添加子菜单
      </van-button>
    </div>

    <div v-if="!loading && !children.length" class="m-empty">
      <span class="m-empty__text">暂无子菜单</span>
    </div>

    <van-cell-group inset>
      <van-swipe-cell v-for="item in children" :key="item.id">
        <van-cell :title="item.name" :label="item.path || ''" is-link @click="goEdit(item.id)">
          <template #icon>
            <span style="font-size:14px;margin-right:6px">📄</span>
          </template>
        </van-cell>
        <template #right>
          <van-button square type="danger" text="删除" @click="handleDelete(item)" />
        </template>
      </van-swipe-cell>
    </van-cell-group>
  </div>
</template>

<style scoped lang="scss">
.m-toolbar {
  padding: 8px 12px;
}
</style>
