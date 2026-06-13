<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as menuApi from '@/api/menus'

const route = useRoute()
const router = useRouter()
const isEdit = computed(() => !!route.params.id)
const menuId = computed(() => route.params.id as string)
const queryParentId = (route.query.parentId as string) || undefined

const loading = ref(false)
const saving = ref(false)

// 可选父级
const parentOptions = ref<{ label: string, value: string }[]>([])

const form = reactive({
  name: '',
  type: 1 as number,
  parentId: '' as string | undefined,
  path: '',
  icon: '',
  permissionCode: '',
  sortOrder: 100,
  isVisible: true,
})

/** 加载页面数据 */
async function loadData() {
  loading.value = true
  try {
    const res = await menuApi.getMenuList()
    const all = res.data ?? []
    // 只展示目录类型（type===1）作为可选父级
    parentOptions.value = all
      .filter(m => (m.type || 0) === 1 && (!isEdit.value || m.id !== menuId.value))
      .map(m => ({ label: m.name, value: m.id }))

    // 从 URL query 获取默认父级
    if (!isEdit.value && queryParentId) {
      form.parentId = queryParentId
      form.type = 2 // 默认页面类型
    }

    if (isEdit.value && menuId.value) {
      const menuRes = await menuApi.getMenuById(menuId.value)
      const m = menuRes.data
      Object.assign(form, {
        name: m.name,
        type: (m.children !== undefined || m.type === 1) ? 1 : 2,
        parentId: m.parentId || undefined,
        path: m.path || '',
        icon: m.icon || '',
        permissionCode: m.permissionCode || '',
        sortOrder: m.sort || 100,
        isVisible: m.isVisible ?? true,
      })
    }
  }
  catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** Save */
async function handleSave() {
  if (!form.name) { ElMessage.warning('请输入菜单名称'); return }
  saving.value = true
  try {
    if (isEdit.value) {
      await menuApi.updateMenu(menuId.value, { name: form.name, path: form.path || undefined, icon: form.icon || undefined, permissionCode: form.permissionCode || undefined, sortOrder: form.sortOrder, isVisible: form.isVisible })
      ElMessage.success('保存成功')
    }
    else {
      await menuApi.createMenu({ name: form.name, type: form.type, parentId: form.parentId || undefined, path: form.path || undefined, icon: form.icon || undefined, permissionCode: form.permissionCode || undefined, sortOrder: form.sortOrder, isVisible: form.isVisible })
      ElMessage.success('创建成功')
    }
    router.back()
  }
  catch { ElMessage.error('操作失败') }
  finally { saving.value = false }
}

/** Delete */
async function handleDelete() {
  try { await ElMessageBox.confirm('确定删除该菜单吗？', '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  try { await menuApi.deleteMenu(menuId.value); ElMessage.success('已删除'); router.back() }
  catch { ElMessage.error('删除失败') }
}

onMounted(loadData)
</script>

<template>
  <div class="m-page">
    <van-nav-bar :title="isEdit ? '编辑菜单' : '新增菜单'" left-arrow fixed placeholder @click-left="router.back()" />

    <van-form v-if="!loading" style="margin-top:8px">
      <van-cell-group inset>
        <van-field v-model="form.name" label="名称" placeholder="请输入菜单名称" :rules="[{ required: true }]" />
      </van-cell-group>

      <van-cell-group inset style="margin-top:12px">
        <van-field label="类型">
          <template #input>
            <van-radio-group v-model="form.type" direction="horizontal" :disabled="isEdit">
              <van-radio :name="1">
                目录
              </van-radio>
              <van-radio :name="2">
                页面
              </van-radio>
            </van-radio-group>
          </template>
        </van-field>
      </van-cell-group>

      <van-cell-group inset style="margin-top:12px">
        <van-field label="上级菜单">
          <template #input>
            <van-dropdown-menu active-color="var(--color-primary)">
              <van-dropdown-item v-model="form.parentId" :options="parentOptions" />
            </van-dropdown-menu>
          </template>
        </van-field>
        <van-field v-model="form.path" label="路由" placeholder="如 /users" />
        <van-field v-model="form.icon" label="图标" placeholder="Tabler图标名" />
        <van-field v-model="form.permissionCode" label="权限编码" placeholder="如 users.list" />
        <van-field v-model="form.sortOrder" label="排序" type="number" />
        <van-field label="可见">
          <template #input>
            <van-switch v-model="form.isVisible" />
          </template>
        </van-field>
      </van-cell-group>
    </van-form>

    <div class="detail-actions">
      <van-button type="primary" round block :loading="saving" @click="handleSave">
        保存
      </van-button>
      <van-button v-if="isEdit" type="danger" round block @click="handleDelete">
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
