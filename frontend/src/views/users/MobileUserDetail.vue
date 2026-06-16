<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as userApi from '@/api/users'
import { UserType } from '@/types/user'
import { parseTime } from '@/utils/index'

const route = useRoute()
const router = useRouter()
const userId = route.params.id as string
const loading = ref(true)
const user = ref<any>(null)

const userTypeLabel: Record<number, string> = {
  [UserType.PlatformAdmin]: '平台管理员',
  [UserType.TenantAdmin]: '租户管理员',
  [UserType.TenantUser]: '租户用户',
}

async function loadDetail() {
  loading.value = true
  try { const res = await userApi.getUserById(userId); user.value = res.data }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

function goEdit() { router.push(`/m/users/${userId}/edit`) }

async function handleToggle() {
  if (!user.value)
    return
  try { await userApi.toggleUser(userId); ElMessage.success(user.value.isActive ? '已禁用' : '已启用'); user.value.isActive = !user.value.isActive }
  catch { ElMessage.error('操作失败') }
}

async function handleDelete() {
  try { await ElMessageBox.confirm(`确定删除用户 "${user.value?.username}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  try { await userApi.deleteUser(userId); ElMessage.success('已删除'); router.back() }
  catch { ElMessage.error('删除失败') }
}

onMounted(loadDetail)
</script>

<template>
  <div class="m-page">
    <van-nav-bar title="用户详情" left-arrow fixed placeholder @click-left="router.back()" />

    <van-skeleton :loading="loading" :row="7">
      <van-cell-group inset>
        <van-cell title="用户名" :value="user?.username" />
        <van-cell title="邮箱" :value="user?.email || '-'" />
        <van-cell title="手机号" :value="user?.phoneNumber || '-'" />
        <van-cell title="用户类型">
          <template #value>
            <van-tag type="primary" size="medium">
              {{ userTypeLabel[user?.userType] || '未知' }}
            </van-tag>
          </template>
        </van-cell>
        <van-cell title="状态">
          <template #value>
            <van-tag :type="user?.isActive ? 'success' : 'danger'">
              {{ user?.isActive ? '启用' : '禁用' }}
            </van-tag>
          </template>
        </van-cell>
        <van-cell title="角色" :value="(user?.roles || []).join(' / ') || '-'" />
        <van-cell title="部门" :value="(user?.organizationUnits || []).map((o: any) => o.name).join(' / ') || '-'" />
        <van-cell title="创建时间" :value="parseTime(user?.createdAt)" />
      </van-cell-group>
    </van-skeleton>

    <div class="detail-actions">
      <van-button type="primary" round block @click="goEdit">
        修改
      </van-button>
      <van-button :type="user?.isActive ? 'warning' : 'success'" round block @click="handleToggle">
        {{ user?.isActive ? '禁用用户' : '启用用户' }}
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
