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

### 3. 平台管理员

`userType === 1`（PlatformAdmin）的用户在 ID 列显示时有额外可见性：
```html
<el-table-column v-if="auth.isSuperAdmin" prop="id" label="ID" />
```

---

> **后端权限文档**：[docs/permissions.md](../../docs/permissions.md) — RBAC 三层模型、权限判定链路、种子数据、Redis 缓存策略
