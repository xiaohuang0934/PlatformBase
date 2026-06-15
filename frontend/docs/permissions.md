# 权限映射 / Permissions

## 权限码体系

后端通过 `[Permission("code")]` 属性控制 API 访问。

| 模块 | 列表 | 创建 | 编辑 | 删除 |
|------|------|------|------|------|
| 用户 | `users.list` | `users.create` | `users.edit` | `users.delete` |
| 角色 | `roles.list` | `roles.create` | `roles.edit` | `roles.delete` |
| 权限 | `perms.list` | `perms.create` | `perms.edit` | `perms.delete` |
| 菜单 | `menus.list` | `menus.create` | `menus.edit` | `menus.delete` |
| 租户 | `tenants.list` | `tenants.create` | `tenants.edit` | `tenants.delete` |
| 字典 | `datadict.list` | `datadict.create` | `datadict.edit` | `datadict.delete` |
| 参数 | `system-params.list` | `system-params.create` | `system-params.edit` | `system-params.delete` |
| 日志 | `operation-logs.list` | — | — | — |
| 文件 | `files.upload` | — | — | — |
| 组织 | `org-units.list` | `org-units.create` | `org-units.edit` | `org-units.delete` |
| 任务 | `jobs.list` / `jobs.manage` | — | — | — |

## v1.8 权限码使用明细

新端点复用了已有权限码（无新增权限码），但权限范围有所扩展：

| 端点 | 所需权限 | 说明 |
|------|---------|------|
| `GET /users/{id}/organizations` | `users.list` | 查看部门 |
| `PUT /users/{id}/organizations` | `users.edit` | 分配部门 |
| `POST /users/{id}/organizations/{orgId}` | `users.edit` | 添加用户到部门 |
| `DELETE /users/{id}/organizations/{orgId}` | `users.edit` | 移出部门 |
| `GET /org-units/{id}/users` | `org-units.list` | 查询部门用户 |
| `GET /org-units/{id}/users/with-children` | `org-units.list` | 分页查询子级用户 |
| `GET /tenants/accessible` | 仅认证 | 平台用户可访问租户列表 |

## 前端权限控制

### 1. 路由级

`src/router/permission.ts` — `beforeEach` 守卫根据菜单树动态注册路由

### 2. 按钮级

```html
<el-button v-permission="'users.create'">新增用户</el-button>
<el-button v-permission="['users.create', 'users.edit']">操作</el-button>
```

`v-permission:some` 表示任一权限满足即可：
```html
<el-button v-permission:some="['users.list', 'roles.list']">查看</el-button>
```

### 3. 用户类型判断（v1.8）

JWT 不再内嵌角色，用户类型通过 `user_type` Claim + profile 接口获取：

```ts
// auth store 中存储 userType
interface AuthState {
  token: string
  userId: string | null
  username: string | null
  userType: number | null   // 1=PlatformAdmin / 2=TenantAdmin / 3=TenantUser
  isSuperAdmin: boolean     // 来自 super_admin claim
  permissions: string[]     // 来自 GET /auth/permissions
}

// 平台管理员可见性控制
<el-table-column v-if="auth.userType === 1" prop="id" label="ID" />

// 超级管理员（不推荐使用，权限检查已内置放行）
<el-button v-if="auth.isSuperAdmin">全局操作</el-button>
```

### 4. 租户切换（v1.8）

平台用户通过 `GET /tenants/accessible` 获取可访问租户列表，前端展示租户选择器：

```ts
// 切换租户 -> 重新获取权限列表 + 菜单树
async function switchTenant(tenantId: string | null) {
  await http.put('/api/v1/tenant/switch', { tenantId })
  // 重新加载菜单树（查询条件自动带上当前租户）
  await permissionStore.generateRoutes()
}
```

---

> **后端权限文档**：[docs/permissions.md](../../docs/permissions.md) — RBAC 三层模型、权限判定链路、种子数据、Redis 缓存策略
