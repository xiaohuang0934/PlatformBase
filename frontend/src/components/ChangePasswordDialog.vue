<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { changePassword } from '@/api/auth'

const visible = defineModel<boolean>('visible', { required: true })
const formRef = ref<FormInstance>()
const submitting = ref(false)

const form = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })

function validateConfirm(_rule: any, value: string, callback: (error?: Error) => void) {
  if (value !== form.newPassword)
    callback(new Error('两次密码不一致'))
  else callback()
}

const rules: FormRules = {
  currentPassword: [{ required: true, message: '请输入当前密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, max: 30, message: '密码长度 6-30 位', trigger: 'blur' },
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    { validator: validateConfirm, trigger: 'blur' },
  ],
}

async function handleSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid)
    return
  submitting.value = true
  try {
    await changePassword({ currentPassword: form.currentPassword, newPassword: form.newPassword })
    ElMessage.success('密码修改成功')
    visible.value = false
  }
  catch {
    ElMessage.error('密码修改失败，请检查当前密码是否正确')
  }
  finally { submitting.value = false }
}
</script>

<template>
  <el-dialog title="修改密码" :model-value="visible" width="420px" destroy-on-close @close="visible = false">
    <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
      <el-form-item label="当前密码" prop="currentPassword">
        <el-input v-model="form.currentPassword" type="password" show-password />
      </el-form-item>
      <el-form-item label="新密码" prop="newPassword">
        <el-input v-model="form.newPassword" type="password" show-password />
      </el-form-item>
      <el-form-item label="确认密码" prop="confirmPassword">
        <el-input v-model="form.confirmPassword" type="password" show-password />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="visible = false">
        取消
      </el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">
        确定
      </el-button>
    </template>
  </el-dialog>
</template>
