<script setup lang="ts">
import { ArrowLeft, Delete, Edit, Plus } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import * as dictApi from '@/api/data-dict'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const loading = ref(false)
const typeList = ref<any[]>([])
const total = ref(0)
const query = reactive({ keyword: '', pageIndex: 1, pageSize: 10 })

/** 获取 Types */
async function fetchTypes() {
  loading.value = true
  try {
    const res = await dictApi.getDictTypes({ keyword: query.keyword || undefined, pageIndex: query.pageIndex, pageSize: query.pageSize })
    typeList.value = res.data.items ?? []; total.value = res.data.totalCount ?? 0
  } catch { ElMessage.error('加载失败，请重试') }
  finally { loading.value = false }
}
/** 分页切换 */
function onPageChange(p: number) { query.pageIndex = p; fetchTypes() }

// --- 类型 CRUD ---
const typeDialog = ref(false); const isTypeEdit = ref(false)
const typeFormRef = ref<FormInstance>()
const typeForm = reactive({ id: '', typeName: '', typeCode: '', description: '' })
const typeSubmitting = ref(false)
const typeRules: FormRules = { typeName: [{ required: true }], typeCode: [{ required: true }] }

/** 打开 Type Create */
function openTypeCreate() { isTypeEdit.value = false; Object.assign(typeForm, { id: '', typeName: '', typeCode: '', description: '' }); typeDialog.value = true }
/** 打开 Type Edit */
function openTypeEdit(row: any) { isTypeEdit.value = true; Object.assign(typeForm, { id: row.id, typeName: row.typeName, typeCode: row.typeCode, description: row.description || '' }); typeDialog.value = true }
/** Type Submit */
async function handleTypeSubmit() {
  const valid = await typeFormRef.value?.validate().catch(() => false)
  if (!valid) return; typeSubmitting.value = true
  try { if (isTypeEdit.value) { await dictApi.updateDictType(typeForm.id, { typeName: typeForm.typeName, description: typeForm.description || undefined }); ElMessage.success('更新成功') } else { await dictApi.createDictType({ typeName: typeForm.typeName, typeCode: typeForm.typeCode, description: typeForm.description || undefined }); ElMessage.success('创建成功') }; typeDialog.value = false; fetchTypes() }
  catch { ElMessage.error('操作失败') } finally { typeSubmitting.value = false }
}
/** Type Delete */
async function handleTypeDelete(row: any) { try { await ElMessageBox.confirm(`确定删除 "${row.typeName}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) } catch { return }; await dictApi.deleteDictType(row.id); ElMessage.success('已删除'); fetchTypes() }

// --- 字典项 ---
const showItems = ref(false); const currentType = ref<any>(null); const itemList = ref<any[]>([])
/** 打开 Items */
async function openItems(row: any) { currentType.value = row; showItems.value = true; await fetchItems() }
/** 返回字典类型列表 */
function backToTypes() { showItems.value = false; currentType.value = null }
/** 获取 Items */
async function fetchItems() { const res = await dictApi.getDictItems(currentType.value.id); itemList.value = res.data ?? [] }

const itemDialog = ref(false); const isItemEdit = ref(false)
const itemFormRef = ref<FormInstance>()
const itemForm = reactive({ id: '', itemName: '', itemCode: '', itemValue: '', sortOrder: 100, parentId: '' as string | undefined })
const itemSubmitting = ref(false)
const itemRules: FormRules = { itemName: [{ required: true }], itemCode: [{ required: true }] }
const parentItemOptions = computed(() => itemList.value.map((m: any) => ({ label: m.itemName, value: m.id })))

/** 打开 Item Create */
function openItemCreate() { isItemEdit.value = false; Object.assign(itemForm, { id: '', itemName: '', itemCode: '', itemValue: '', sortOrder: 100, parentId: undefined }); itemDialog.value = true }
/** 打开 Item Edit */
function openItemEdit(row: any) { isItemEdit.value = true; Object.assign(itemForm, { id: row.id, itemName: row.itemName, itemCode: row.itemCode, itemValue: row.itemValue || '', sortOrder: row.sortOrder ?? 100, parentId: row.parentId || undefined }); itemDialog.value = true }
/** Item Submit */
async function handleItemSubmit() {
  const valid = await itemFormRef.value?.validate().catch(() => false)
  if (!valid) return; itemSubmitting.value = true
  try { if (isItemEdit.value) { await dictApi.updateDictItem(itemForm.id, { itemName: itemForm.itemName, itemCode: itemForm.itemCode, itemValue: itemForm.itemValue || undefined, sortOrder: itemForm.sortOrder }); ElMessage.success('更新成功') } else { await dictApi.createDictItem({ dictTypeId: currentType.value.id, itemName: itemForm.itemName, itemCode: itemForm.itemCode, itemValue: itemForm.itemValue || undefined, sortOrder: itemForm.sortOrder, parentId: itemForm.parentId || undefined }); ElMessage.success('创建成功') }; itemDialog.value = false; fetchItems() }
  catch { ElMessage.error('操作失败') } finally { itemSubmitting.value = false }
}
/** Item Delete */
async function handleItemDelete(row: any) { try { await ElMessageBox.confirm(`确定删除 "${row.itemName}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) } catch { return }; await dictApi.deleteDictItem(row.id); ElMessage.success('已删除'); fetchItems() }
</script>

<template>
  <!-- 字典类型列表 -->
  <div v-if="!showItems" class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        数据字典
      </h2><el-button type="primary" :icon="Plus" @click="openTypeCreate">
        新增类型
      </el-button>
    </div>
    <el-table v-loading="loading" :data="typeList" border stripe row-key="id">
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="typeName" label="名称" min-width="140" />
      <el-table-column prop="typeCode" label="编码" width="140" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="操作" width="240" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" @click="openItems(row)">
            字典项
          </el-button>
          <el-button type="primary" link size="small" :icon="Edit" @click="openTypeEdit(row)">
            编辑
          </el-button>
          <el-button type="danger" link size="small" :icon="Delete" @click="handleTypeDelete(row)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>
    <div style="display:flex;justify-content:flex-end;margin-top:16px">
      <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" :total="total" :page-sizes="[10, 20, 50]" layout="total,sizes,prev,pager,next" @current-change="onPageChange" @size-change="onPageChange" />
    </div>
  </div>

  <!-- 字典项列表 -->
  <div v-else class="page-container">
    <div class="page-header">
      <div style="display:flex;align-items:center;gap:12px">
        <el-button :icon="ArrowLeft" @click="backToTypes">
          返回
        </el-button>
        <h2 class="page-header__title">
          {{ currentType?.name }} — 字典项
        </h2>
      </div>
      <el-button type="primary" :icon="Plus" @click="openItemCreate">
        新增字典项
      </el-button>
    </div>
    <el-table :data="itemList" border stripe row-key="id">
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column prop="itemName" label="名称" min-width="140" />
      <el-table-column prop="itemCode" label="编码" min-width="140" />
      <el-table-column prop="itemValue" label="值" min-width="140" />
      <el-table-column prop="sortOrder" label="排序" width="80" align="center" />
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button type="primary" link size="small" :icon="Edit" @click="openItemEdit(row)">
            编辑
          </el-button>
          <el-button type="danger" link size="small" :icon="Delete" @click="handleItemDelete(row)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>

  <!-- 类型弹窗 -->
  <el-dialog v-model="typeDialog" :title="isTypeEdit ? '编辑类型' : '新增类型'" width="480px" destroy-on-close @closed="typeFormRef?.resetFields()">
    <el-form ref="typeFormRef" :model="typeForm" :rules="typeRules" label-width="80px">
      <el-form-item label="名称" prop="typeName">
        <el-input v-model="typeForm.typeName" placeholder="请输入名称" />
      </el-form-item>
      <el-form-item label="编码" prop="typeCode">
        <el-input v-model="typeForm.typeCode" :disabled="isTypeEdit" placeholder="请输入编码" />
      </el-form-item>
      <el-form-item label="描述">
        <el-input v-model="typeForm.description" type="textarea" :rows="3" placeholder="请输入描述" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="typeDialog = false">
        取消
      </el-button><el-button type="primary" :loading="typeSubmitting" @click="handleTypeSubmit">
        确定
      </el-button>
    </template>
  </el-dialog>

  <!-- 字典项弹窗 -->
  <el-dialog v-model="itemDialog" :title="isItemEdit ? '编辑字典项' : '新增字典项'" width="480px" destroy-on-close @closed="itemFormRef?.resetFields()">
    <el-form ref="itemFormRef" :model="itemForm" :rules="itemRules" label-width="80px">
      <el-form-item label="名称" prop="itemName">
        <el-input v-model="itemForm.itemName" placeholder="请输入名称" />
      </el-form-item>
      <el-form-item label="编码" prop="itemCode">
        <el-input v-model="itemForm.itemCode" placeholder="请输入编码" />
      </el-form-item>
      <el-form-item label="值">
        <el-input v-model="itemForm.itemValue" placeholder="请输入值" />
      </el-form-item>
      <el-form-item label="排序">
        <el-input-number v-model="itemForm.sortOrder" :min="0" :step="10" />
      </el-form-item>
      <el-form-item label="上级">
        <el-select v-model="itemForm.parentId" placeholder="无（顶级）" clearable style="width:100%">
          <el-option v-for="p in parentItemOptions" :key="p.value" :label="p.label" :value="p.value" />
        </el-select>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="itemDialog = false">
        取消
      </el-button><el-button type="primary" :loading="itemSubmitting" @click="handleItemSubmit">
        确定
      </el-button>
    </template>
  </el-dialog>
</template>
