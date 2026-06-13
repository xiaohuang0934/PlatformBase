<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as dictApi from '@/api/data-dict'

const route = useRoute(); const router = useRouter(); const id = route.params.id as string
const loading = ref(true); const item = ref<any>(null)
/** 加载数据 */
async function load() {
  loading.value = true; try { const res = await dictApi.getDictItems(id); item.value = res.data }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}
onMounted(load)

// 数据字典只有删除，修改在action-sheet
async function handleDelete() {
  try { await ElMessageBox.confirm('确定删除吗？', '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }; try { await dictApi.deleteDictType(id); ElMessage.success('已删除'); router.back() }
  catch { ElMessage.error('删除失败') }
}
</script>

<template>
  <div class="m-page">
    <van-nav-bar title="字典详情" left-arrow fixed placeholder @click-left="router.back()" />
    <van-skeleton :loading="loading" :row="3">
      <van-cell-group inset>
        <van-cell v-for="v in (item || [])" :key="v.id" :title="v.itemName" :label="v.itemCode" :value="v.itemValue" />
      </van-cell-group>
    </van-skeleton>
    <div class="detail-actions">
      <van-button type="danger" round block @click="handleDelete">
        删除
      </van-button>
    </div>
  </div>
</template>

<style scoped lang="scss">
.detail-actions {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
</style>
