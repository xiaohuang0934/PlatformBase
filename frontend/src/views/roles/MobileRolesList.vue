<script setup lang="ts">
import type { RoleDto } from '@/types/auth'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as roleApi from '@/api/roles'
import { parseTime } from '@/utils/index'

const router = useRouter(); const loading = ref(false); const list = ref<RoleDto[]>([]); const finished = ref(false)
const keyword = ref(''); const pageIndex = ref(1)
/** 获取 List */
async function fetchList() {
  loading.value = true; try {
    const res = await roleApi.getRoleList({ keyword: keyword.value || undefined, pageIndex: pageIndex.value, pageSize: 10 }); const items = res.data.items; if (pageIndex.value === 1)
      list.value = items; else list.value.push(...items); finished.value = items.length < 10
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}
/** 搜索 */
function onSearch() { pageIndex.value = 1; fetchList() }
/** 滚动加载更多 */
function onLoad() { pageIndex.value++; fetchList() }
/** 跳转到详情页 */
function goDetail(id: string) { router.push(`/m/roles/${id}`) }
onMounted(fetchList)

const showForm = ref(false); const form = reactive({ name: '', code: '', description: '' }); const submitting = ref(false)
/** 打开 Create */
function openCreate() { Object.assign(form, { name: '', code: '', description: '' }); showForm.value = true }
/** Create */
async function handleCreate() {
  if (!form.name || !form.code)
    return; submitting.value = true; try { await roleApi.createRole({ name: form.name, code: form.code, description: form.description || undefined }); ElMessage.success('创建成功'); showForm.value = false; pageIndex.value = 1; list.value = []; fetchList() }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}
</script>

<template>
  <div class="m-page">
    <van-sticky>
      <van-search v-model="keyword" placeholder="搜索名称/编码" shape="round" @search="onSearch" @clear="onSearch" />
    </van-sticky>
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
            <span class="card-header__title">{{ item.name }} <van-icon name="arrow" size="14" color="var(--color-text-dim)" /></span><van-tag :type="item.isSystem ? 'primary' : ''" size="medium">
              {{ item.isSystem ? '系统' : '自定义' }}
            </van-tag>
          </div>
          <div class="card-row">
            <span class="card-row__label">编码</span><span>{{ item.code }}</span>
          </div>
          <div v-if="item.description" class="card-row">
            <span class="card-row__label">描述</span><span>{{ item.description }}</span>
          </div>
          <div class="card-row">
            <span class="card-row__label">创建</span><span>{{ parseTime(item.createdAt) }}</span>
            <div class="card-footer">
              <div class="card-footer" />
            </div>
          </div>
        </div>
      </div>
    </van-list>
    <van-action-sheet v-model:show="showForm" title="新增角色">
      <div style="padding:16px">
        <van-field v-model="form.name" label="名称" placeholder="请输入角色名称" /><van-field v-model="form.code" label="编码" placeholder="请输入角色编码" /><van-field v-model="form.description" label="描述" type="textarea" autosize /><van-button round block type="primary" :loading="submitting" style="margin-top:16px" @click="handleCreate">
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
