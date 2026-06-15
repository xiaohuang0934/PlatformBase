<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import * as dictApi from '@/api/data-dict'

interface DictType {
  id: string
  typeName: string
  typeCode: string
  description: string | null
  sortOrder: number
}

interface DictItem {
  id: string
  itemName: string
  itemCode: string
  itemValue: string | null
  parentId: string | null
  sortOrder: number
  children: DictItem[]
}

const loading = ref(false)
const typeList = ref<DictType[]>([])
const expandedTypeIds = ref<Set<string>>(new Set())
const typeItems = ref<Record<string, DictItem[]>>({})

const keyword = ref('')

const filteredTypes = computed(() => {
  if (!keyword.value) return typeList.value
  const kw = keyword.value.toLowerCase()
  return typeList.value.filter(t =>
    t.typeName.toLowerCase().includes(kw) || t.typeCode.toLowerCase().includes(kw),
  )
})

async function fetchTypes() {
  loading.value = true
  try {
    const res = await dictApi.getDictTypes({ pageSize: 200 })
    typeList.value = (res.data?.items || []) as DictType[]
  }
  catch { ElMessage.error('加载字典类型失败') }
  finally { loading.value = false }
}

async function toggleType(typeId: string) {
  if (expandedTypeIds.value.has(typeId)) {
    expandedTypeIds.value.delete(typeId)
  }
  else {
    expandedTypeIds.value.add(typeId)
    if (!typeItems.value[typeId]) {
      try {
        const res = await dictApi.getDictItems(typeId)
        typeItems.value[typeId] = (res.data || []) as DictItem[]
      }
      catch { ElMessage.error('加载字典项失败') }
    }
  }
  expandedTypeIds.value = new Set(expandedTypeIds.value)
}

// ───── 类型表单 ─────
const typeDialogVisible = ref(false)
const typeDialogTitle = ref('新增字典类型')
const isTypeEdit = ref(false)
const typeFormRef = ref<FormInstance>()
const typeSubmitting = ref(false)
const typeForm = reactive({ id: '', typeName: '', typeCode: '', description: '', sortOrder: 0 })
const typeRules: FormRules = {
  typeName: [{ required: true, message: '请输入类型名称', trigger: 'blur' }],
  typeCode: [{ required: true, message: '请输入类型编码', trigger: 'blur' }],
}

function openCreateType() {
  isTypeEdit.value = false
  typeDialogTitle.value = '新增字典类型'
  Object.assign(typeForm, { id: '', typeName: '', typeCode: '', description: '', sortOrder: 0 })
  typeDialogVisible.value = true
}

function openEditType(row: DictType) {
  isTypeEdit.value = true
  typeDialogTitle.value = '编辑字典类型'
  Object.assign(typeForm, { id: row.id, typeName: row.typeName, typeCode: row.typeCode, description: row.description || '', sortOrder: row.sortOrder || 0 })
  typeDialogVisible.value = true
}

async function handleTypeSubmit() {
  const valid = await typeFormRef.value?.validate().catch(() => false)
  if (!valid) return
  typeSubmitting.value = true
  try {
    if (isTypeEdit.value) {
      await dictApi.updateDictType(typeForm.id, { typeName: typeForm.typeName, description: typeForm.description || undefined, sortOrder: typeForm.sortOrder })
      ElMessage.success('更新成功')
    }
    else {
      await dictApi.createDictType({ typeName: typeForm.typeName, typeCode: typeForm.typeCode, description: typeForm.description || undefined, sortOrder: typeForm.sortOrder })
      ElMessage.success('创建成功')
    }
    typeDialogVisible.value = false
    fetchTypes()
  }
  catch { ElMessage.error('操作失败') }
  finally { typeSubmitting.value = false }
}

async function handleDeleteType(row: DictType) {
  try { await ElMessageBox.confirm(`确定删除字典类型 "${row.typeName}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await dictApi.deleteDictType(row.id)
  ElMessage.success('已删除')
  fetchTypes()
}

// ───── 字典项表单 ─────
const itemDialogVisible = ref(false)
const itemDialogTitle = ref('新增字典项')
const isItemEdit = ref(false)
const itemFormRef = ref<FormInstance>()
const itemSubmitting = ref(false)
const itemForm = reactive({ id: '', dictTypeId: '', itemName: '', itemCode: '', itemValue: '', parentId: '' as string | undefined, sortOrder: 0 })
const itemRules: FormRules = {
  itemName: [{ required: true, message: '请输入项名称', trigger: 'blur' }],
  itemCode: [{ required: true, message: '请输入项编码', trigger: 'blur' }],
}

function openCreateItem(dictTypeId: string, parentId?: string) {
  isItemEdit.value = false
  itemDialogTitle.value = parentId ? '新增子项' : '新增字典项'
  Object.assign(itemForm, { id: '', dictTypeId, itemName: '', itemCode: '', itemValue: '', parentId: parentId || undefined, sortOrder: 0 })
  itemDialogVisible.value = true
}

function openEditItem(dictTypeId: string, row: DictItem) {
  isItemEdit.value = true
  itemDialogTitle.value = '编辑字典项'
  Object.assign(itemForm, { id: row.id, dictTypeId, itemName: row.itemName, itemCode: row.itemCode, itemValue: row.itemValue || '', parentId: row.parentId || undefined, sortOrder: row.sortOrder || 0 })
  itemDialogVisible.value = true
}

async function handleItemSubmit() {
  const valid = await itemFormRef.value?.validate().catch(() => false)
  if (!valid) return
  itemSubmitting.value = true
  try {
    if (isItemEdit.value) {
      await dictApi.updateDictItem(itemForm.id, { itemName: itemForm.itemName, itemValue: itemForm.itemValue || undefined, sortOrder: itemForm.sortOrder })
      ElMessage.success('更新成功')
    }
    else {
      await dictApi.createDictItem({ dictTypeId: itemForm.dictTypeId, itemName: itemForm.itemName, itemCode: itemForm.itemCode, itemValue: itemForm.itemValue || undefined, parentId: itemForm.parentId, sortOrder: itemForm.sortOrder })
      ElMessage.success('创建成功')
    }
    itemDialogVisible.value = false
    refreshItems(itemForm.dictTypeId)
  }
  catch { ElMessage.error('操作失败') }
  finally { itemSubmitting.value = false }
}

async function handleDeleteItem(dictTypeId: string, row: DictItem) {
  try { await ElMessageBox.confirm(`确定删除字典项 "${row.itemName}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await dictApi.deleteDictItem(row.id)
  ElMessage.success('已删除')
  refreshItems(dictTypeId)
}

async function refreshItems(dictTypeId: string) {
  try {
    const res = await dictApi.getDictItems(dictTypeId)
    typeItems.value[dictTypeId] = (res.data || []) as DictItem[]
  }
  catch { }
}

/** 获取 Item Name */
function getItemIndent(level: number) {
  return { paddingLeft: `${32 + level * 20}px` }
}

/** 渲染字典项列表（递归） */
function renderItems(typeId: string, items: DictItem[], level: number): any[] {
  const result: any[] = []
  for (const item of items) {
    result.push({ ...item, _level: level, _typeId: typeId })
    if (item.children?.length)
      result.push(...renderItems(typeId, item.children, level + 1))
  }
  return result
}

function onSearch() { /* filteredTypes 自动响应 */ }
function onReset() { keyword.value = '' }

onMounted(fetchTypes)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        数据字典
      </h2>
      <el-button type="primary" :icon="Plus" @click="openCreateType">
        新增类型
      </el-button>
    </div>

    <!-- 搜索栏 -->
    <div class="search-bar">
      <el-input v-model="keyword" placeholder="搜索类型名称/编码" clearable style="width: 240px" @keyup.enter="onSearch" />
      <el-button type="primary" @click="onSearch">
        搜索
      </el-button>
      <el-button @click="onReset">
        重置
      </el-button>
    </div>

    <!-- 字典类型树 -->
    <div v-loading="loading" class="dict-tree">
      <template v-for="t in filteredTypes" :key="t.id">
        <div class="tree-type">
          <div class="tree-type__row" @click="toggleType(t.id)">
            <span class="tree-type__expand">
              <span class="tree-type__arrow">
                {{ expandedTypeIds.has(t.id) ? '▼' : '▶' }}
              </span>
            </span>
            <span class="tree-type__name">{{ t.typeName }}</span>
            <span class="tree-type__code">{{ t.typeCode }}</span>
            <div class="tree-type__actions">
              <el-button type="primary" link size="small" :icon="Plus" @click.stop="openCreateItem(t.id)">
                新增项
              </el-button>
              <el-button type="primary" link size="small" :icon="Edit" @click.stop="openEditType(t)">
                编辑
              </el-button>
              <el-button type="danger" link size="small" :icon="Delete" @click.stop="handleDeleteType(t)">
                删除
              </el-button>
            </div>
          </div>

          <!-- 字典项列表 -->
          <template v-if="expandedTypeIds.has(t.id)">
            <template v-for="item in renderItems(t.id, typeItems[t.id] || [], 0)" :key="item.id">
              <div class="tree-item">
                <div class="tree-item__row" :style="getItemIndent(item._level)">
                  <span class="tree-item__name">{{ item.itemName }}</span>
                  <span class="tree-item__code">{{ item.itemCode }}</span>
                  <span v-if="item.itemValue" class="tree-item__value">= {{ item.itemValue }}</span>
                  <div class="tree-item__actions">
                    <el-button type="primary" link size="small" :icon="Plus" @click.stop="openCreateItem(item._typeId, item.id)">
                      子项
                    </el-button>
                    <el-button type="primary" link size="small" :icon="Edit" @click.stop="openEditItem(item._typeId, item)">
                      编辑
                    </el-button>
                    <el-button type="danger" link size="small" :icon="Delete" @click.stop="handleDeleteItem(item._typeId, item)">
                      删除
                    </el-button>
                  </div>
                </div>
              </div>
            </template>
          </template>
        </div>
      </template>

      <div v-if="!loading && filteredTypes.length === 0" class="dict-tree__empty">
        暂无数据
      </div>
    </div>
  </div>

  <!-- 类型弹窗 -->
  <el-dialog v-model="typeDialogVisible" :title="typeDialogTitle" width="480px" destroy-on-close @closed="typeFormRef?.resetFields()">
    <el-form ref="typeFormRef" :model="typeForm" :rules="typeRules" label-width="80px">
      <el-form-item label="名称" prop="typeName">
        <el-input v-model="typeForm.typeName" placeholder="请输入类型名称" />
      </el-form-item>
      <el-form-item label="编码" prop="typeCode">
        <el-input v-model="typeForm.typeCode" :disabled="isTypeEdit" placeholder="请输入类型编码" />
      </el-form-item>
      <el-form-item label="描述">
        <el-input v-model="typeForm.description" type="textarea" :rows="2" placeholder="请输入描述" />
      </el-form-item>
      <el-form-item label="排序">
        <el-input-number v-model="typeForm.sortOrder" :min="0" :step="10" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="typeDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="typeSubmitting" @click="handleTypeSubmit">确定</el-button>
    </template>
  </el-dialog>

  <!-- 字典项弹窗 -->
  <el-dialog v-model="itemDialogVisible" :title="itemDialogTitle" width="480px" destroy-on-close @closed="itemFormRef?.resetFields()">
    <el-form ref="itemFormRef" :model="itemForm" :rules="itemRules" label-width="80px">
      <el-form-item label="名称" prop="itemName">
        <el-input v-model="itemForm.itemName" placeholder="请输入项名称" />
      </el-form-item>
      <el-form-item label="编码" prop="itemCode">
        <el-input v-model="itemForm.itemCode" :disabled="isItemEdit" placeholder="请输入项编码" />
      </el-form-item>
      <el-form-item label="值">
        <el-input v-model="itemForm.itemValue" placeholder="请输入值（可选）" />
      </el-form-item>
      <el-form-item label="排序">
        <el-input-number v-model="itemForm.sortOrder" :min="0" :step="10" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="itemDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="itemSubmitting" @click="handleItemSubmit">确定</el-button>
    </template>
  </el-dialog>
</template>

<style scoped lang="scss">
.dict-tree {
  background: $color-bg-card;
  border: 1px solid $color-border;
  border-radius: $radius-lg;
  padding: $spacing-sm 0;

  &__empty {
    text-align: center;
    padding: 40px 0;
    color: $color-text-dim;
    font-size: $font-size-md;
  }
}

.tree-type {
  &__row {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
    padding: 12px $spacing-md;
    cursor: pointer;
    transition: background $transition-fast;
    border-bottom: 1px solid $color-border;

    &:hover {
      background: $color-bg-hover;
    }
  }

  &__expand {
    width: 20px;
    flex-shrink: 0;
  }

  &__arrow {
    font-size: 10px;
    color: $color-text-dim;
  }

  &__name {
    font-weight: 600;
    font-size: $font-size-md;
    color: $color-text-primary;
  }

  &__code {
    font-size: $font-size-sm;
    color: $color-text-dim;
  }

  &__actions {
    margin-left: auto;
    display: flex;
    gap: 4px;
    flex-shrink: 0;
  }
}

.tree-item {
  &__row {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
    padding: 8px $spacing-md;
    transition: background $transition-fast;

    &:hover {
      background: $color-bg-hover;
    }
  }

  &__name {
    font-weight: 500;
    color: $color-text-primary;
    min-width: 80px;
  }

  &__code {
    font-size: $font-size-sm;
    color: $color-text-dim;
  }

  &__value {
    font-size: $font-size-sm;
    color: $color-primary;
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
