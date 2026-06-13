<script setup lang="ts">
import type { PermissionDto } from '@/types/auth'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as permApi from '@/api/permissions'

const router = useRouter(); const loading = ref(false); const list = ref<PermissionDto[]>([]); const finished = ref(false)
const keyword = ref(''); const pageIndex = ref(1)
async function fetchList() {
  loading.value = true; try {
    const res = await permApi.getPermissionList({ keyword: keyword.value || undefined, pageIndex: pageIndex.value, pageSize: 10 }); const items = res.data.items; if (pageIndex.value === 1)
      list.value = items; else list.value.push(...items); finished.value = items.length < 10
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}
function onSearch() { pageIndex.value = 1; fetchList() }
function onLoad() { pageIndex.value++; fetchList() }
function goDetail(id: string) { router.push(`/m/permissions/${id}`) }
onMounted(fetchList)

const showForm = ref(false); const form = reactive({ name: '', code: '', group: '', description: '' }); const submitting = ref(false)
function openCreate() { Object.assign(form, { name: '', code: '', group: '', description: '' }); showForm.value = true }
async function handleCreate() {
  if (!form.name || !form.code)
    return; submitting.value = true; try { await permApi.createPermission({ name: form.name, code: form.code, group: form.group || undefined, description: form.description || undefined }); ElMessage.success('创建成功'); showForm.value = false; pageIndex.value = 1; list.value = []; fetchList() }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}
</script>

<template>
  <div class="m-page">
    <van-sticky><van-search v-model="keyword" placeholder="搜索名称/编码" shape="round" @search="onSearch" @clear="onSearch" /></van-sticky>
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

    <van-list v-model:loading="loading" :finished="finished" finished-text="没有更多了" @load="onLoad">
      <div class="m-card-list">
        <div v-for="item in list" :key="item.id" class="m-card-list__item" @click="goDetail(item.id)">
          <div class="card-header">
            <span class="card-header__title">{{ item.name }} <van-icon name="arrow" size="14" color="var(--color-text-dim)" /></span><van-tag type="primary" size="medium">
              {{ item.group || '-' }}
            </van-tag>
          </div>
          <div class="card-row">
            <span class="card-row__label">编码</span><span>{{ item.code }}</span>
          </div>
          <div v-if="item.description" class="card-row">
            <span class="card-row__label">描述</span><span>{{ item.description }}</span>
            <div class="card-footer">
              <div class="card-footer" />
            </div>
          </div>
        </div>
      </div>
    </van-list>
    <van-action-sheet v-model:show="showForm" title="新增权限">
      <div style="padding:16px">
        <van-field v-model="form.name" label="名称" /><van-field v-model="form.code" label="编码" /><van-field v-model="form.group" label="分组" /><van-field v-model="form.description" label="描述" type="textarea" autosize /><van-button round block type="primary" :loading="submitting" style="margin-top:16px" @click="handleCreate">
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
