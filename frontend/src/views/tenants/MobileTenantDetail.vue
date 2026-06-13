<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as tenantApi from '@/api/tenants'
import { parseTime } from '@/utils/index'

const route = useRoute(); const router = useRouter(); const id = route.params.id as string
const loading = ref(true); const item = ref<any>(null)

const form = reactive({ name: '', description: '' }); const saving = ref(false)

/** 加载数据 */
async function load() {
  loading.value = true
  try { const res = await tenantApi.getTenantById(id); item.value = res.data; Object.assign(form, { name: res.data.name, description: res.data.description || '' }) }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** Save */
async function handleSave() {
  if (!form.name)
    return; saving.value = true; try { await tenantApi.updateTenant(id, { name: form.name, description: form.description || undefined }); ElMessage.success('保存成功') }
  catch { ElMessage.error('保存失败') }
  finally { saving.value = false }
}
/** Delete */
async function handleDelete() {
  try { await ElMessageBox.confirm(`确定删除 "${item.value?.name}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }; try { await tenantApi.deleteTenant(id); ElMessage.success('已删除'); router.back() }
  catch { ElMessage.error('删除失败') }
}
onMounted(load)
</script>

<template>
  <div class="m-page">
    <van-nav-bar title="租户编辑" left-arrow fixed placeholder @click-left="router.back()" />
    <van-skeleton :loading="loading" :row="4">
      <van-cell-group inset style="margin-top:8px">
        <van-field v-model="form.name" label="名称" placeholder="请输入租户名称" />
        <van-field v-model="form.description" label="描述" placeholder="请输入描述" type="textarea" autosize />
        <van-cell title="编码" :value="item?.code" />
        <van-cell title="创建时间" :value="parseTime(item?.createdAt)" />
      </van-cell-group>
    </van-skeleton>
    <div class="detail-actions">
      <van-button type="primary" round block :loading="saving" @click="handleSave">
        保存
      </van-button>
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
