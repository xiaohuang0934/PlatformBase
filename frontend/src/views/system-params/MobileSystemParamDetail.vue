<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as paramApi from '@/api/system-params'

const route = useRoute(); const router = useRouter(); const id = route.params.id as string
const loading = ref(true); const item = ref<any>(null)

const form = reactive({ value: '', description: '' }); const saving = ref(false)

async function load() {
  loading.value = true
  try { const res = await paramApi.getParamByCode(id); item.value = res.data; Object.assign(form, { value: res.data.value, description: res.data.description || '' }) }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

async function handleSave() {
  if (!form.value)
    return; saving.value = true; try { await paramApi.updateParam(id, { value: form.value, description: form.description || undefined }); ElMessage.success('保存成功') }
  catch { ElMessage.error('保存失败') }
  finally { saving.value = false }
}
async function handleDelete() {
  try { await ElMessageBox.confirm(`确定删除 "${item.value?.code}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }; try { await paramApi.deleteParam(id); ElMessage.success('已删除'); router.back() }
  catch { ElMessage.error('删除失败') }
}
onMounted(load)
</script>

<template>
  <div class="m-page">
    <van-nav-bar title="参数编辑" left-arrow fixed placeholder @click-left="router.back()" />
    <van-skeleton :loading="loading" :row="4">
      <van-cell-group inset style="margin-top:8px">
        <van-cell title="编码" :value="item?.code" />
        <van-field v-model="form.value" label="值" placeholder="请输入值" />
        <van-field v-model="form.description" label="描述" placeholder="请输入描述" type="textarea" autosize />
        <van-cell title="分类" :value="item?.category || '-'" />
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
