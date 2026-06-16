<script setup lang="ts">
import type { RoleDto } from '@/types/auth'
import { ElMessage } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getOrgUnitTree } from '@/api/organization'
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
const allOrgs = ref<any[]>([])

const form = reactive({
  username: '',
  password: '',
  email: '',
  phoneNumber: '',
  tenantId: undefined as string | undefined,
  userType: undefined as number | undefined,
  roleIds: [] as string[],
  organizationUnitIds: [] as string[],
})

async function loadData() {
  loading.value = true
  try {
    const [rolesRes, orgsRes] = await Promise.all([getRoleList({ pageSize: 200 }), getOrgUnitTree()])
    allRoles.value = rolesRes.data.items
    allOrgs.value = orgsRes.data || []
    if (isEdit.value && userId.value) {
      const res = await userApi.getUserById(userId.value)
      user.value = res.data
      Object.assign(form, {
        username: res.data.username,
        password: '',
        email: res.data.email || '',
        phoneNumber: res.data.phoneNumber || '',
        tenantId: undefined,
        userType: res.data.userType,
        roleIds: res.data.roles || [],
        organizationUnitIds: (res.data.organizationUnits || []).map((o: any) => o.id),
      })
    }
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

async function handleSubmit() {
  if (!form.username) { ElMessage.warning('请输入用户名'); return }
  if (!isEdit.value && !form.password) { ElMessage.warning('请输入密码'); return }
  if (!isEdit.value && !form.roleIds.length) { ElMessage.warning('请选择角色'); return }
  if (!isEdit.value && !form.organizationUnitIds.length) { ElMessage.warning('请选择部门'); return }
  submitting.value = true
  try {
    if (isEdit.value) {
      await userApi.updateUser(userId.value, {
        email: form.email || undefined,
        phoneNumber: form.phoneNumber || undefined,
        userType: form.userType,
        roleIds: form.roleIds.length ? form.roleIds : undefined,
        organizationUnitIds: form.organizationUnitIds.length ? form.organizationUnitIds : undefined,
      })
      ElMessage.success('更新成功')
    }
    else {
      await userApi.createUser({
        username: form.username,
        password: form.password,
        email: form.email || undefined,
        phoneNumber: form.phoneNumber || undefined,
        tenantId: form.tenantId,
        userType: form.userType,
        roleIds: form.roleIds,
        organizationUnitIds: form.organizationUnitIds,
      })
      ElMessage.success('创建成功')
    }
    router.back()
  }
  catch { ElMessage.error('操作失败') }
  finally { submitting.value = false }
}

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

// 角色选择
const showRolePicker = ref(false)
const selectedRoles = ref<string[]>([])
function openRolePicker() {
  selectedRoles.value = [...form.roleIds]
  showRolePicker.value = true
}
function confirmRoles() {
  form.roleIds = selectedRoles.value
  showRolePicker.value = false
}
function toggleRole(roleId: string) {
  const idx = selectedRoles.value.indexOf(roleId)
  if (idx >= 0)
    selectedRoles.value.splice(idx, 1)
  else selectedRoles.value.push(roleId)
}

// 部门选择
const showOrgPicker = ref(false)
const selectedOrgIds = ref<string[]>([])
function openOrgPicker() {
  selectedOrgIds.value = [...form.organizationUnitIds]
  showOrgPicker.value = true
}
function confirmOrgs() {
  form.organizationUnitIds = selectedOrgIds.value
  showOrgPicker.value = false
}
function flattenTree(nodes: any[]): any[] {
  const result: any[] = []
  function walk(list: any[]) {
    for (const n of list) {
      result.push({ id: n.id, name: n.name, code: n.code })
      if (n.children?.length)
        walk(n.children)
    }
  }
  walk(nodes)
  return result
}

function toggleOrg(id: string) {
  const idx = selectedOrgIds.value.indexOf(id)
  if (idx >= 0)
    selectedOrgIds.value.splice(idx, 1)
  else selectedOrgIds.value.push(id)
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
        <van-cell title="角色" :value="form.roleIds.length ? `${form.roleIds.length} 个角色` : '请选择'" is-link @click="openRolePicker" />
      </van-cell-group>

      <van-cell-group inset style="margin-top: 12px">
        <van-cell title="部门" :value="form.organizationUnitIds.length ? `${form.organizationUnitIds.length} 个部门` : '请选择'" is-link @click="openOrgPicker" />
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

    <!-- 角色选择 popup -->
    <van-popup v-model:show="showRolePicker" position="bottom" :style="{ height: '60%' }">
      <div class="picker-header">
        <span @click="showRolePicker = false">取消</span>
        <span class="picker-title">选择角色</span>
        <span style="color: var(--color-primary)" @click="confirmRoles">确定</span>
      </div>
      <van-cell-group>
        <van-cell v-for="role in allRoles" :key="role.id" :title="role.name" clickable @click="toggleRole(role.id)">
          <template #right-icon>
            <van-checkbox :name="role.id" :model-value="selectedRoles.includes(role.id)" />
          </template>
        </van-cell>
      </van-cell-group>
    </van-popup>

    <!-- 部门选择 popup -->
    <van-popup v-model:show="showOrgPicker" position="bottom" :style="{ height: '60%' }">
      <div class="picker-header">
        <span @click="showOrgPicker = false">取消</span>
        <span class="picker-title">选择部门</span>
        <span style="color: var(--color-primary)" @click="confirmOrgs">确定</span>
      </div>
      <van-cell-group>
        <van-cell v-for="org in flattenTree(allOrgs)" :key="org.id" :title="org.name" :label="org.code" clickable @click="toggleOrg(org.id)">
          <template #right-icon>
            <van-checkbox :name="org.id" :model-value="selectedOrgIds.includes(org.id)" />
          </template>
        </van-cell>
      </van-cell-group>
      <div v-if="!allOrgs.length" class="m-empty" style="padding:40px 0">
        <span class="m-empty__text">暂无可选部门，请先创建部门</span>
      </div>
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
