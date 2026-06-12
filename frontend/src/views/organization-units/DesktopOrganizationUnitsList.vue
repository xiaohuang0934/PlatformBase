<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import * as orgApi from '@/api/organization'
import { useAuthStore } from '@/stores/auth'
import { parseTime } from '@/utils/index'

const auth = useAuthStore()
const loading = ref(false)
const list = ref<any[]>([])
const flatList = ref<any[]>([])

function flatten(items: any[]): any[] {
  const result: any[] = []
  for (const item of items) {
    result.push(item)
    if (item.children?.length)
      result.push(...flatten(item.children))
  }
  return result
}

async function fetchList() {
  loading.value = true
  try {
    const res = await orgApi.getOrgUnitTree()
    list.value = res.data ?? []
    flatList.value = flatten(list.value)
  }
  catch {
    ElMessage.error('加载失败，请重试')
  }
  finally { loading.value = false }
}

const dialogVisible = ref(false)
const dialogTitle = ref('新增组织')
const isEditing = ref(false)
const formRef = ref<FormInstance>()
const submitting = ref(false)
const form = reactive({ id: '', name: '', parentId: '' as string | undefined, description: '' })
const rules: FormRules = { name: [{ required: true, message: '请输入组织名称', trigger: 'blur' }] }
const parentOptions = computed(() => flatList.value.map(m => ({ label: m.name, value: m.id })))

function openCreate() { isEditing.value = false; dialogTitle.value = '新增组织'; Object.assign(form, { id: '', name: '', parentId: undefined, description: '' }); dialogVisible.value = true }
function openEdit(row: any) { isEditing.value = true; dialogTitle.value = '编辑组织'; Object.assign(form, { id: row.id, name: row.name, parentId: row.parentId || undefined, description: row.description || '' }); dialogVisible.value = true }

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid)
    return; submitting.value = true
  try {
    if (isEditing.value) { await orgApi.updateOrgUnit(form.id, { name: form.name, description: form.description || undefined }); ElMessage.success('更新成功') }
    else { await orgApi.createOrgUnit({ name: form.name, parentId: form.parentId, description: form.description || undefined }); ElMessage.success('创建成功') }
    dialogVisible.value = false; fetchList()
  }
  finally { submitting.value = false }
}

async function handleDelete(row: any) {
  try { await ElMessageBox.confirm(`确定删除 "${row.name}" 吗？`, '确认删除', { confirmButtonText: '删除', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  await orgApi.deleteOrgUnit(row.id); ElMessage.success('已删除'); fetchList()
}

onMounted(fetchList)
</script>

<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-header__title">
        组织架构
      </h2><el-button type="primary" :icon="Plus" @click="openCreate">
        新增组织
      </el-button>
    </div>
    <el-table v-loading="loading" :data="flatList" border stripe row-key="id">
      <el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" width="280" show-overflow-tooltip />
      <el-table-column label="名称" min-width="200">
        <template #default="{ row }">
          <span :style="{ paddingLeft: `${getIndent(row) * 24}px` }">{{ row.name }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">
          {{ parseTime(row.createdAt) }}
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
  <el-dialog v-model="dialogVisible" :title="dialogTitle" width="480px" destroy-on-close @closed="formRef?.resetFields()">
    <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" placeholder="请输入组织名称" />
      </el-form-item>
      <el-form-item label="上级">
        <el-select v-model="form.parentId" placeholder="无（顶级组织）" clearable style="width:100%">
          <el-option v-for="p in parentOptions" :key="p.value" :label="p.label" :value="p.value" />
        </el-select>
      </el-form-item>
      <el-form-item label="描述">
        <el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入描述" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="dialogVisible = false">
        取消
      </el-button><el-button type="primary" :loading="submitting" @click="handleSubmit">
        确定
      </el-button>
    </template>
  </el-dialog>
</template>
