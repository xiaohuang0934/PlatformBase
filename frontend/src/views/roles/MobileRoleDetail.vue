<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as roleApi from '@/api/roles'
import { parseTime } from '@/utils/index'

const route = useRoute(); const router = useRouter(); const id = route.params.id as string
const loading = ref(true); const role = ref<any>(null)

const form = reactive({ name: '', description: '' }); const saving = ref(false)

/** 加载数据 */
async function load() {
  loading.value = true
  try { const res = await roleApi.getRoleById(id); role.value = res.data; Object.assign(form, { name: res.data.name, description: res.data.description || '' }) }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** Save */
async function handleSave() {
  if (!form.name)
    return; saving.value = true
  try { await roleApi.updateRole(id, { name: form.name, description: form.description || undefined }); ElMessage.success('保存成功'); load() }
  catch { ElMessage.error('保存失败') }
  finally { saving.value = false }
}

/** Delete */
async function handleDelete() {
  try { await ElMessageBox.confirm(`确定删除 "${role.value?.name}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  try { await roleApi.deleteRole(id); ElMessage.success('已删除'); router.back() }
  catch { ElMessage.error('删除失败') }
}

onMounted(load)
</script>

<template>
  <div class="m-page">
    <van-nav-bar title="角色编辑" left-arrow fixed placeholder @click-left="router.back()" />
    <van-skeleton :loading="loading" :row="4">
      <van-cell-group inset style="margin-top:8px">
        <van-field v-model="form.name" label="名称" placeholder="请输入角色名称" />
        <van-field v-model="form.description" label="描述" placeholder="请输入描述" type="textarea" autosize />
        <van-cell title="编码" :value="role?.code" />
        <van-cell title="类型">
          <template #value>
            <van-tag :type="role?.isSystem ? 'primary' : ''" size="medium">
              {{ role?.isSystem ? '系统' : '自定义' }}
            </van-tag>
          </template>
        </van-cell>
        <van-cell title="创建时间" :value="parseTime(role?.createdAt)" />
      </van-cell-group>
    </van-skeleton>
    <div class="detail-actions">
      <van-button type="primary" round block :loading="saving" @click="handleSave">
        保存
      </van-button>
      <van-button v-if="!role?.isSystem" type="danger" round block @click="handleDelete">
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
