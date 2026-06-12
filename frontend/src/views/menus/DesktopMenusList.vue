<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import type { MenuDto } from '@/types/auth'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import * as menuApi from '@/api/menus'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const loading = ref(false)
const menuList = ref<MenuDto[]>([])

/** 扁平化树，建立 id → name 映射 */
const nameMap = ref<Record<string, string>>({})

async function fetchList() {
  loading.value = true
  try {
    const res = await menuApi.getMenuList()
    const flat: MenuDto[] = []
    function walk(items: MenuDto[]) {
      for (const item of items) {
        flat.push(item)
        nameMap.value[item.id] = item.name
        if (item.children?.length)
          walk(item.children)
      }
    }
    walk(res.data)
    menuList.value = flat
  }
  finally {
    loading.value = false
  }
}

// --- 新增/编辑 ---
const dialogVisible = ref(false)
const dialogTitle = ref('新增菜单')
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const submitting = ref(false)

const form = reactive({
  id: '',
  name: '',
  type: 1 as number,
  parentId: '' as string | undefined,
  path: '',
  icon: '',
  permissionCode: '',
  sortOrder: 100,
  isVisible: true,
})

const formRules: FormRules = {
  name: [{ required: true, message: '请输入菜单名称', trigger: 'blur' }],
}

// 可选父级（只允许目录作为父级）
const parentOptions = computed(() =>
  menuList.value
    .filter(m => m.children !== undefined || m.type === 1)
    .map(m => ({ label: m.name, value: m.id })),
)

function openCreate() {
  isEditing.value = false
  dialogTitle.value = '新增菜单'
  Object.assign(form, { id: '', name: '', type: 1, parentId: undefined, path: '', icon: '', permissionCode: '', sortOrder: 100, isVisible: true })
  dialogVisible.value = true
}

function openEdit(row: MenuDto) {
  isEditing.value = true
  dialogTitle.value = '编辑菜单'
  Object.assign(form, {
    id: row.id,
    name: row.name,
    type: row.type ?? 1,
    parentId: row.parentId || undefined,
    path: row.path || '',
    icon: row.icon || '',
    permissionCode: row.permissionCode || '',
    sortOrder: row.sort || 100,
    isVisible: row.isVisible ?? true,
  })
  dialogVisible.value = true
}

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid)
    return

  submitting.value = true
  try {
    if (isEditing.value) {
      await menuApi.updateMenu(form.id, {
        name: form.name,
        path: form.path || undefined,
        icon: form.icon || undefined,
        permissionCode: form.permissionCode || undefined,
        sortOrder: form.sortOrder,
        isVisible: form.isVisible,
      })
      ElMessage.success('更新成功')
    }
    else {
      await menuApi.createMenu({
        name: form.name,
        type: form.type,
        parentId: form.parentId || undefined,
        path: form.path || undefined,
        icon: form.icon || undefined,
        permissionCode: form.permissionCode || undefined,
        sortOrder: form.sortOrder,
        isVisible: form.isVisible,
      })
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false
    fetchList()
  }
  finally {
    submitting.value = false
  }
}

async function handleDelete(row: MenuDto) {
  try {
    await ElMessageBox.confirm(`确定删除菜单 "${row.name}" 吗？`, '确认删除', {
      confirmButtonText: '删除',
      cancelButtonText: '取消',
      type: 'warning',
    })
  }
  catch {
    return
  }
  await menuApi.deleteMenu(row.id)
  ElMessage.success('已删除')
  fetchList()
}

function getParentName(parentId: string | null): string {
  if (!parentId)
    return '-'
  return nameMap.value[parentId] || parentId
}

function getLevel(item: MenuDto): number {
  return item.path ? 2 : 1
}

onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        菜单管理
      </h2>
      <el-button type="primary" :icon="Plus" @click="openCreate">
        新增菜单
      </el-button>
    </div>

    <el-table v-loading="loading" :data="menuList" border stripe row-key="id">
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="name" label="名称" min-width="140" />
      <el-table-column label="类型" width="80" align="center">
        <template #default="{ row }">
          <el-tag size="small" :type="getLevel(row) === 1 ? '' : 'info'">
            {{ getLevel(row) === 1 ? '目录' : '页面' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="上级菜单" width="120">
        <template #default="{ row }">
          {{ getParentName(row.parentId) }}
        </template>
      </el-table-column>
      <el-table-column prop="path" label="路由" width="150" show-overflow-tooltip />
      <el-table-column prop="icon" label="图标" width="120" />
      <el-table-column prop="permissionCode" label="权限编码" width="150" show-overflow-tooltip />
      <el-table-column prop="sort" label="排序" width="70" align="center" />
      <el-table-column label="可见" width="70" align="center">
        <template #default="{ row }">
          <el-tag :type="row.isVisible ? 'success' : 'danger'" size="small">
            {{ row.isVisible ? '是' : '否' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" :icon="Edit" @click="openEdit(row)">
            编辑
          </el-button>
          <el-button type="danger" link size="small" :icon="Delete" @click="handleDelete(row)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>

  <el-dialog v-model="dialogVisible" :title="dialogTitle" width="520px" destroy-on-close @closed="formRef?.resetFields()">
    <el-form ref="formRef" :model="form" :rules="formRules" label-width="90px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" placeholder="请输入菜单名称" />
      </el-form-item>
      <el-form-item label="类型">
        <el-radio-group v-model="form.type" :disabled="isEditing">
          <el-radio :value="1">
            目录
          </el-radio>
          <el-radio :value="2">
            页面
          </el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="上级菜单">
        <el-select v-model="form.parentId" placeholder="无（顶级菜单）" clearable style="width: 100%">
          <el-option v-for="p in parentOptions" :key="p.value" :label="p.label" :value="p.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="路由路径">
        <el-input v-model="form.path" placeholder="如 /users" />
      </el-form-item>
      <el-form-item label="图标">
        <el-input v-model="form.icon" placeholder="Element Plus 图标名，如 UserFilled" />
      </el-form-item>
      <el-form-item label="权限编码">
        <el-input v-model="form.permissionCode" placeholder="如 users.list" />
      </el-form-item>
      <el-form-item label="排序">
        <el-input-number v-model="form.sortOrder" :min="0" :step="10" />
      </el-form-item>
      <el-form-item label="可见">
        <el-switch v-model="form.isVisible" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="dialogVisible = false">
        取消
      </el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">
        确定
      </el-button>
    </template>
  </el-dialog>
</template>
