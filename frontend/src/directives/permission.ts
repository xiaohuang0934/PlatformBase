import type { Directive, DirectiveBinding } from 'vue'
import { useAuthStore } from '@/stores/auth'

/**
 * v-permission 指令 — 按钮级权限控制
 *
 * 用法:
 *   <el-button v-permission="'users.create'">新增用户</el-button>
 *   <el-button v-permission="['users.create', 'users.edit']">操作</el-button>
 *   <el-button v-permission:some="'users.create'">任一权限</el-button>
 */
export const permission: Directive = {
  mounted(el: HTMLElement, binding: DirectiveBinding) {
    const auth = useAuthStore()
    const codes = Array.isArray(binding.value) ? binding.value : [binding.value]
    const mode = binding.arg === 'some' ? 'some' : 'every'

    const hasAccess = mode === 'some'
      ? codes.some(code => auth.hasPermission(code))
      : codes.every(code => auth.hasPermission(code))

    if (!hasAccess) {
      el.parentNode?.removeChild(el)
    }
  },
}

/**
 * v-user-type 指令 — 根据用户类型控制可见性
 *
 * 用法:
 *   <el-button v-user-type="1">仅平台管理员可见</el-button>
 *   <el-button v-user-type="[1, 2]">平台/租户管理员可见</el-button>
 */
export const userType: Directive = {
  mounted(el: HTMLElement, binding: DirectiveBinding) {
    const auth = useAuthStore()
    const types = Array.isArray(binding.value) ? binding.value : [binding.value]
    const currentType = auth.user?.userType

    if (!types.includes(currentType)) {
      el.parentNode?.removeChild(el)
    }
  },
}
