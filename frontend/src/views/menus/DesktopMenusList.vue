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
const menuTree = ref<MenuDto[]>([])
const expandedIds = ref<Set<string>>(new Set())

/** 获取 List */
async function fetchList() {
  loading.value = true
  try {
    const res = await menuApi.getMenuList()
    const all = res.data ?? []
    const parents = all.filter(m => !m.parentId)
    menuTree.value = parents.map(p => ({
      ...p,
      children: all.filter(m => m.parentId === p.id),
    }))
  } catch { ElMessage.error('加载失败') }
  finally { loading.value = false }
}

/** 切换展开/折叠状态 */
function toggleExpand(id: string) {
  if (expandedIds.value.has(id))
    expandedIds.value.delete(id)
  else expandedIds.value.add(id)
  expandedIds.value = new Set(expandedIds.value)
}

// --- 新增/编辑弹窗 ---
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

/** 可选父级（目录） */
const parentOptions = computed(() => {
  const result: { label: string, value: string }[] = []
  /** Walk */
  function walk(items: MenuDto[], depth = 0) {
    for (const item of items) {
      result.push({ label: `${'─'.repeat(depth)} ${item.name}`, value: item.id })
      if (item.children?.length)
        walk(item.children, depth + 1)
    }
  }
  walk(menuTree.value)
  return result
})

/** 打开 Create */
function openCreate(parentId?: string) {
  isEditing.value = false; dialogTitle.value = '新增菜单'
  Object.assign(form, { id: '', name: '', type: 1, parentId: parentId || undefined, path: '', icon: '', permissionCode: '', sortOrder: 100, isVisible: true })
  dialogVisible.value = true
}

/** 打开 Edit */
function openEdit(row: any) {
  isEditing.value = true; dialogTitle.value = '编辑菜单'
  Object.assign(form, {
    id: row.id,
    name: row.name,
    type: (row.children !== undefined || row.type === 1) ? 1 : 2,
    parentId: row.parentId || undefined,
    path: row.path || '',
    icon: row.icon || '',
    permissionCode: row.permissionCode || '',
    sortOrder: row.sort || 100,
    isVisible: row.isVisible ?? true,
  })
  dialogVisible.value = true
}

/** Submit */
async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid)
    return; submitting.value = true
  try {
    if (isEditing.value) {
      await menuApi.updateMenu(form.id, { name: form.name, path: form.path || undefined, icon: form.icon || undefined, permissionCode: form.permissionCode || undefined, sortOrder: form.sortOrder, isVisible: form.isVisible })
      ElMessage.success('更新成功')
    }
    else {
      await menuApi.createMenu({ name: form.name, type: form.type, parentId: form.parentId || undefined, path: form.path || undefined, icon: form.icon || undefined, permissionCode: form.permissionCode || undefined, sortOrder: form.sortOrder, isVisible: form.isVisible })
      ElMessage.success('创建成功')
    }
    dialogVisible.value = false; fetchList()
  }
  catch { ElMessage.error('操作失败，请重试') }
  finally { submitting.value = false }
}

/** Delete */
async function handleDelete(row: any) {
  try { await ElMessageBox.confirm(`确定删除菜单 "${row.name}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await menuApi.deleteMenu(row.id); ElMessage.success('已删除'); fetchList()
}

/** 添加 Child */
function addChild(parentId: string) { openCreate(parentId) }

/** 获取 Type Label */
function getTypeLabel(item: any): string {
  return (item.children !== undefined || item.type === 1) ? '目录' : '页面'
}

onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        菜单管理
      </h2>
      <el-button type="primary" :icon="Plus" @click="openCreate()">
        新增菜单
      </el-button>
    </div>

    <!-- 树形菜单列表 -->
    <div v-loading="loading" class="menu-tree">
      <template v-for="item in menuTree" :key="item.id">
        <!-- 一级菜单 -->
        <div class="tree-item tree-item--level0" @click="toggleExpand(item.id)">
          <div class="tree-item__row">
            <span class="tree-item__expand">
              <span v-if="item.children?.length" class="tree-item__arrow">{{ expandedIds.has(item.id) ? '▼' : '▶' }}</span>
            </span>
            <span class="tree-item__name">{{ item.name }}</span>
            <el-tag size="small" :type="getTypeLabel(item) === '目录' ? undefined : 'info'">
              {{ getTypeLabel(item) }}
            </el-tag>
            <span v-if="item.permissionCode" class="tree-item__perm">{{ item.permissionCode }}</span>
            <div class="tree-item__actions">
              <el-button type="primary" link size="small" :icon="Edit" @click.stop="openEdit(item)">
                编辑
              </el-button>
              <el-button type="danger" link size="small" :icon="Delete" @click.stop="handleDelete(item)">
                删除
              </el-button>
              <el-button v-if="getTypeLabel(item) === '目录'" type="primary" link size="small" :icon="Plus" @click.stop="addChild(item.id)">
                子菜单
              </el-button>
            </div>
          </div>

          <!-- 子菜单 -->
          <template v-if="expandedIds.has(item.id) && item.children?.length">
            <div v-for="child in item.children" :key="child.id" class="tree-item tree-item--level1">
              <div class="tree-item__row">
                <span class="tree-item__indent" />
                <span class="tree-item__name">{{ child.name }}</span>
                <el-tag size="small" type="info">
                  页面
                </el-tag>
                <span v-if="child.path" class="tree-item__path">{{ child.path }}</span>
                <span v-if="child.permissionCode" class="tree-item__perm">{{ child.permissionCode }}</span>
                <div class="tree-item__actions">
                  <el-button type="primary" link size="small" :icon="Edit" @click.stop="openEdit(child)">
                    编辑
                  </el-button>
                  <el-button type="danger" link size="small" :icon="Delete" @click.stop="handleDelete(child)">
                    删除
                  </el-button>
                </div>
              </div>
            </div>
          </template>
        </div>
      </template>
    </div>
  </div>

  <!-- 新增/编辑弹窗（复用） -->
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
        <el-select v-model="form.parentId" placeholder="无（顶级菜单）" clearable style="width:100%">
          <el-option v-for="p in parentOptions" :key="p.value" :label="p.label" :value="p.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="路由路径">
        <el-input v-model="form.path" placeholder="如 /users" />
      </el-form-item>
      <el-form-item label="图标">
        <el-input v-model="form.icon" placeholder="Tabler 图标名，如 IconUsers" />
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

<style scoped lang="scss">
.menu-tree {
  background: $color-bg-card;
  border: 1px solid $color-border;
  border-radius: $radius-lg;
  padding: $spacing-sm 0;
}

.tree-item {
  &__row {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
    padding: 10px $spacing-md;
    transition: background $transition-fast;
    &:hover {
      background: $color-bg-hover;
    }
  }

  &--level1 &__row {
    padding-left: 48px;
  }

  &__expand {
    width: 20px;
    cursor: pointer;
    color: $color-text-dim;
    flex-shrink: 0;
  }

  &__indent {
    width: 28px;
    flex-shrink: 0;
  }

  &__name {
    font-weight: 500;
    color: $color-text-primary;
    min-width: 100px;
  }

  &__perm {
    font-size: $font-size-sm;
    color: $color-text-dim;
  }

  &__path {
    font-size: $font-size-sm;
    color: $color-text-dim;
    font-family: $font-mono;
  }

  &__actions {
    margin-left: auto;
    display: flex;
    gap: 4px;
    flex-shrink: 0;
  }
}
</style>
