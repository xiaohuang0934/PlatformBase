<script setup lang="ts">
import type { RoleDto } from '@/types/auth'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getRoleList } from '@/api/roles'
import * as userApi from '@/api/users'

const route = useRoute()
const router = useRouter()
const isEdit = computed(() => !!route.params.id)
const userId = computed(() => route.params.id as string)

const loading = ref(false)
const submitting = ref(false)
const user = ref<any>(null)
const allRoles = ref<RoleDto[]>([])

const form = reactive({
  username: '',
  password: '',
  email: '',
  phoneNumber: '',
  roleIds: [] as string[],
})

/** 加载页面数据 */
async function loadData() {
  loading.value = true
  try {
    const [rolesRes] = await Promise.all([getRoleList({ pageSize: 200 })])
    allRoles.value = rolesRes.data.items
    if (isEdit.value && userId.value) {
      const res = await userApi.getUserById(userId.value)
      user.value = res.data
      Object.assign(form, {
        username: res.data.username,
        password: '',
        email: res.data.email || '',
        phoneNumber: res.data.phoneNumber || '',
        roleIds: res.data.roles || [],
      })
    }
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** Submit */
async function handleSubmit() {
  if (!form.username) { ElMessage.warning('请输入用户名'); return }
  if (!isEdit.value && !form.password) { ElMessage.warning('请输入密码'); return }
  submitting.value = true
  try {
    if (isEdit.value) {
      await userApi.updateUser(userId.value, { email: form.email || undefined, phoneNumber: form.phoneNumber || undefined, roleIds: form.roleIds })
      ElMessage.success('更新成功')
    }
    else {
      await userApi.createUser({ username: form.username, password: form.password, email: form.email || undefined, phoneNumber: form.phoneNumber || undefined, roleIds: form.roleIds })
      ElMessage.success('创建成功')
    }
    router.back()
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

/** Toggle */
async function handleToggle() {
  if (!user.value)
    return
  try {
    await userApi.toggleUser(userId.value)
    ElMessage.success(user.value.isActive ? '已禁用' : '已启用')
    user.value.isActive = !user.value.isActive
  }
  catch { ElMessage.error('操作失败') }
}

const showRolePicker = ref(false)
const selectedRoles = ref<string[]>([])

/** 打开 Role Picker */
function openRolePicker() {
  selectedRoles.value = [...form.roleIds]
  showRolePicker.value = true
}

/** 确认角色选择 */
function confirmRoles() {
  form.roleIds = selectedRoles.value
  showRolePicker.value = false
}

onMounted(loadData)
</script>

<template>
  <div class="m-page">
    <van-nav-bar :title="isEdit ? '编辑用户' : '新增用户'" left-arrow fixed placeholder @click-left="$router.back" />

    <van-form v-if="!loading" style="margin-top: 8px" @submit="handleSubmit">
      <van-cell-group inset>
        <van-field v-model="form.username" label="用户名" placeholder="请输入用户名" :disabled="isEdit" :rules="[{ required: true, message: '请输入用户名' }]" />
        <van-field v-if="!isEdit" v-model="form.password" label="密码" type="password" placeholder="请输入密码" :rules="[{ required: true, message: '请输入密码' }]" />
        <van-field v-model="form.email" label="邮箱" placeholder="请输入邮箱" />
        <van-field v-model="form.phoneNumber" label="手机号" placeholder="请输入手机号" />
      </van-cell-group>

      <van-cell-group inset style="margin-top: 12px">
        <van-cell title="角色" :value="form.roleIds.length ? `${form.roleIds.length} 个角色` : '未选择'" is-link @click="openRolePicker" />
      </van-cell-group>

      <div style="padding: 16px">
        <van-button round block type="primary" native-type="submit" :loading="submitting">
          {{ isEdit ? '保存' : '创建' }}
        </van-button>
        <van-button v-if="isEdit" round block type="danger" style="margin-top: 12px" @click="handleToggle">
          {{ user?.isActive ? '禁用用户' : '启用用户' }}
        </van-button>
      </div>
    </van-form>

    <!-- 角色选择 -->
    <van-popup v-model:show="showRolePicker" position="bottom" :style="{ height: '60%' }">
      <div class="picker-header">
        <span @click="showRolePicker = false">取消</span>
        <span class="picker-title">选择角色</span>
        <span style="color: var(--color-primary)" @click="confirmRoles">确定</span>
      </div>
      <van-checkbox-group v-model="selectedRoles">
        <van-cell-group>
          <van-cell v-for="role in allRoles" :key="role.id" :title="role.name" clickable @click="() => { const idx = selectedRoles.indexOf(role.id); if (idx >= 0) selectedRoles.splice(idx, 1); else selectedRoles.push(role.id) }">
            <template #right-icon>
              <van-checkbox :name="role.id" />
            </template>
          </van-cell>
        </van-cell-group>
      </van-checkbox-group>
    </van-popup>
  </div>
</template>

<style scoped lang="scss">
.picker-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  font-size: $font-size-md;
  border-bottom: 1px solid $header-border;
}
.picker-title {
  font-weight: 600;
  color: $color-text-primary;
}
</style>
