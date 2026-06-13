<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as orgApi from '@/api/organization'

const router = useRouter(); const loading = ref(false); const list = ref<any[]>([]); const flatList = ref<any[]>([])
/** 扁平化树形数据 */
function flatten(items: any[]): any[] {
  const r: any[] = []; for (const item of items) {
    r.push(item); if (item.children?.length)
      r.push(...flatten(item.children))
  } return r
}
/** 获取 List */
async function fetchList() {
  loading.value = true; try { const res = await orgApi.getOrgUnitTree(); list.value = res.data ?? []; flatList.value = flatten(list.value) }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}
/** 获取 Indent */
function getIndent(item: any): number { return (item.path?.split('/').length ?? 1) - 1 }
/** 跳转到详情页 */
function goDetail(id: string) { router.push(`/m/organization-units/${id}`) }
onMounted(fetchList)

const showForm = ref(false); const form = reactive({ name: '', parentId: '' as string | undefined, description: '' }); const submitting = ref(false)
/** 打开 Create */
function openCreate() { Object.assign(form, { name: '', parentId: undefined, description: '' }); showForm.value = true }
/** Create */
async function handleCreate() {
  if (!form.name)
    return; submitting.value = true; try { await orgApi.createOrgUnit({ name: form.name, parentId: form.parentId, description: form.description || undefined }); ElMessage.success('创建成功'); showForm.value = false; fetchList() }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}
</script>

<template>
  <div class="m-page">
    <div class="m-toolbar">
      <van-button type="primary" block round @click="openCreate">
        添加
      </van-button>
    </div>
    <!-- 空状态 -->
    <div v-if="list.length === 0 && !loading" class="m-empty">
      <span class="m-empty__icon">📋</span>
      <span class="m-empty__text">暂无数据</span>
    </div>

    <div class="m-card-list">
      <div v-for="item in flatList" :key="item.id" class="m-card-list__item" @click="goDetail(item.id)">
        <div class="card-header">
          <span class="card-header__title" :style="{ paddingLeft: `${getIndent(item) * 16}px` }">{{ item.name }} <van-icon name="arrow" size="14" color="var(--color-text-dim)" /></span>
        </div>
        <div v-if="item.description" class="card-row">
          <span class="card-row__label">描述</span><span>{{ item.description }}</span>
        </div>
      </div>
    </div>
    <van-action-sheet v-model:show="showForm" title="新增组织">
      <div style="padding:16px">
        <van-field v-model="form.name" label="名称" /><van-field v-model="form.description" label="描述" type="textarea" autosize /><van-button round block type="primary" :loading="submitting" style="margin-top:16px" @click="handleCreate">
          确定
        </van-button>
      </div>
    </van-action-sheet>
  </div>
</template>

<style scoped lang="scss">
.m-toolbar {
  padding: 8px 12px;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
  &__title {
    font-size: $font-size-md;
    font-weight: 600;
    color: $color-text-primary;
  }
}
.card-row {
  display: flex;
  gap: 8px;
  font-size: $font-size-sm;
  color: $color-text-regular;
  padding: 2px 0;
  &__label {
    color: $color-text-dim;
    min-width: 40px;
  }
}
</style>

.card-footer { display: flex; align-items: center; justify-content: center; gap: 4px; margin-top: 10px; padding-top: 8px; border-top: 1px solid var(--color-border); } .card-footer__link { font-size: 12px; color: var(--color-text-dim); }
