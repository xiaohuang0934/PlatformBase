<script setup lang="ts">
import type { FormInstance, FormRules } from 'element-plus'
import { Lock, UserFilled } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useDevice } from '@/composables/useDevice'
import { useAuthStore } from '@/stores/auth'
import { usePermissionStore } from '@/stores/permission'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const permission = usePermissionStore()
const { isMobile } = useDevice()

const formRef = ref<FormInstance>()
const loading = ref(false)

const loginForm = reactive({ username: '', password: '' })

const rules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function handleLogin() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid)
    return
  loading.value = true
  try {
    await auth.loginAction(loginForm)
    await auth.fetchCurrentUser()
    await permission.fetchMenus()
    router.push((route.query.redirect as string) || '/')
  }
  catch (err: any) {
    ElMessage.error(err?.response?.data?.message || err?.message || '登录失败')
  }
  finally { loading.value = false }
}
</script>

<template>
  <div class="login-stage" :class="{ 'login-stage--mobile': isMobile }">
    <div class="login-card">
      <div class="login-card__header">
        <h1 class="login-card__title">
          PlatformBase
        </h1>
        <p class="login-card__subtitle">
          企业级通用开发底座
        </p>
      </div>

      <el-form
        ref="formRef"
        :model="loginForm"
        :rules="rules"
        label-position="top"
        class="login-form"
        @keyup.enter="handleLogin"
      >
        <el-form-item prop="username">
          <el-input
            v-model="loginForm.username"
            placeholder="用户名"
            :prefix-icon="UserFilled"
            size="large"
          />
        </el-form-item>

        <el-form-item prop="password">
          <el-input
            v-model="loginForm.password"
            type="password"
            placeholder="密码"
            :prefix-icon="Lock"
            size="large"
            show-password
          />
        </el-form-item>

        <el-form-item>
          <el-button
            type="primary"
            size="large"
            :loading="loading"
            class="login-btn"
            @click="handleLogin"
          >
            登 录
          </el-button>
        </el-form-item>
      </el-form>
    </div>
  </div>
</template>

<style scoped lang="scss">
.login-stage {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  background: $color-bg-deep;
}

.login-stage--mobile {
  background: $color-bg-base;
}

.login-card {
  width: 400px;
  padding: $spacing-2xl $spacing-xl $spacing-xl;
  background: $color-bg-card;
  border: 1px solid $color-border;
  border-radius: $radius-xl;
  box-shadow: $shadow-lg;

  &__header {
    text-align: center;
    margin-bottom: $spacing-xl;
  }

  &__title {
    font-family: $font-display;
    font-size: 28px;
    font-weight: 600;
    color: $color-text-primary;
    letter-spacing: 0.03em;
    margin-bottom: $spacing-sm;
  }

  &__subtitle {
    font-size: $font-size-sm;
    color: $color-text-dim;
    letter-spacing: 0.06em;
  }
}

.login-form {
  :deep(.el-input__wrapper) {
    background: $color-bg-base;
    border: 1px solid $color-border;
    border-radius: $radius-md;
    box-shadow: none;
    padding: 8px 12px;
    transition: border-color $transition-fast;

    &:hover {
      border-color: $color-border-accent;
    }
    &.is-focus {
      border-color: $color-primary;
    }
  }

  :deep(.el-input__inner) {
    color: $color-text-primary;
    &::placeholder {
      color: $color-text-placeholder;
    }
  }

  :deep(.el-input__prefix-inner) {
    color: $color-text-dim;
  }

  .login-btn {
    width: 100%;
    height: 44px;
    margin-top: $spacing-sm;
    font-size: $font-size-base;
    font-weight: 500;
    letter-spacing: 0.15em;
    border-radius: $radius-md;
  }
}
</style>
