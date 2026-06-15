# 权限管理 / Permission Management

## 概述 / Overview

PlatformBase 采用 **三层 RBAC 权限模型**：

```
┌─────────┐     N:M      ┌───────────┐     N:M      ┌─────────────┐
│  Users  │──────────────│   Roles   │──────────────│ Permissions │
└────┬────┘              └───────────┘              │ (API 接口)  │
     │                                              └─────────────┘
     │   N:M (直达权限，优先级 > 角色)                    ▲
     └──────────────────────────────────────────────────┘
```

| 层级 | 表 | 说明 |
|------|-----|------|
| 角色定义 | `Roles` | 继承 `AuditableEntity`，是权限的集合载体 |
| 用户-角色 | `UserRoles` | M:N 关联，用户通过角色继承权限 |
| 权限定义 | `Permissions` | API 接口定义（Code + ResourcePath + HttpMethod） |
| 角色-权限 | `RolePermissions` | M:N 关联，角色拥有的 API 权限 |
| 用户直达 | `UserPermissions` | 单用户权限控制，含 `IsGranted` 覆盖 |

## 权限判定流程 / Permission Resolution

```
请求: DELETE /api/users/xxx
       → [Permission("users.delete")]
         │
         ▼
PermissionAuthorizationHandler
  │
  ├─ ① Redis GET "user:perms:{userId}"
  │      HIT → 判断 "users.delete" in [codes] → 放行/403
  │      MISS ↓
  │
  ├─ ② 查数据库合并:
  │   ┌─────────────────────────────────────────┐
  │   │ Step A: 查 UserPermissions (直达权限)    │
  │   │   IsGranted=false → 从结果中删除该 code  │
  │   │   IsGranted=true  → 加入结果            │
  │   ├─────────────────────────────────────────┤
  │   │ Step B: 查 UserRoles → RolePermissions  │
  │   │   角色继承的 code → 加入结果             │
  │   └─────────────────────────────────────────┘
  │
  ├─ ③ 回写 Redis SET "user:perms:{userId}" [codes] EX 1800
  │
  └─ ④ 匹配 → 放行 / 403
```

## IsGranted 覆盖逻辑 / IsGranted Override

用户直达权限优先级高于角色权限：

```
场景: 张三属于 Admin 角色，Admin 有 users.delete 权限
     但管理员在 UserPermissions 中设置:
       { UserId=张三, PermissionId=users.delete, IsGranted=false }

     角色权限 → users.delete ✓
     用户直达 → IsGranted=false → 从结果中删除
     最终    → ✗ 无此权限
```

## Permission 实体 / Permission Entity

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | `Guid` (PK) | |
| `Code` | `string` UNIQUE | 权限编码，如 `users.create` |
| `Name` | `string` | 显示名 |
| `ResourcePath` | `string` | API 路径，如 `/api/users` |
| `HttpMethod` | `string` | `GET` / `POST` / `PUT` / `DELETE` |
| `GroupName` | `string?` | 展示分组标签，仅管理后台归类用 |
| `SortOrder` | `int` | 排序号 |
| `IsEnabled` | `bool` | 启用/禁用 |
| `Description` | `string?` | 备注 |

## [Permission] 属性 / Permission Attribute

```csharp
// 单权限
[Permission("users.delete")]
public async Task<IActionResult> Delete(Guid id) { ... }

// 多权限（满足其一即可）
[Permission("users.edit")]
[Permission("users.delete")]
public async Task<IActionResult> Manage(Guid id) { ... }
```

**原理**：`PermissionAttribute` 生成策略名 `"Permission:users.delete"` → `PermissionPolicyProvider` 动态创建 `AuthorizationPolicy` → `PermissionAuthorizationHandler` 验证。

## 前端权限获取 / Frontend Permission API

```http
GET /api/auth/permissions
Authorization: Bearer {token}

响应: ["users.list", "users.create", "roles.list"]
```

前端根据返回的 code 数组控制按钮/菜单的显示隐藏：

```javascript
// 示例
if (permissions.includes('users.delete-btn')) {
    showDeleteButton();
}
```

## Redis 缓存策略 / Redis Cache Strategy

| Key | Value | TTL | 写入 | 清除 |
|-----|-------|-----|------|------|
| `user:perms:{userId}` | `[codes]` JSON | 30 min | 鉴权 cache miss 时回写 | 角色/权限变更时 DEL |

**缓存失效时机**：

- 管理员修改角色权限 → 查 `UserRoles.Where(ur => ur.RoleId == roleId)` 获取所有受影响的 UserId → 逐个 `DEL user:perms:{userId}`
- 管理员修改用户直达权限 → `DEL user:perms:{userId}`
- 下次用户请求 → cache miss → 重新查库 → 回写

**降级策略**：Redis 不可用时静默降级到数据库查询，不影响鉴权功能。

## 种子权限数据 / Seed Permissions

| Code | Method | Path | Group | Admin | Manager | User |
|------|--------|------|-------|-------|---------|------|
| `users.list` | GET | `/api/users` | 用户管理 | ✓ | ✓ | ✓ |
| `users.create` | POST | `/api/users` | 用户管理 | ✓ | | |
| `users.edit` | PUT | `/api/users` | 用户管理 | ✓ | | |
| `users.delete` | DELETE | `/api/users` | 用户管理 | ✓ | | |
| `roles.list` | GET | `/api/roles` | 角色管理 | ✓ | ✓ | |
| `roles.create` | POST | `/api/roles` | 角色管理 | ✓ | | |
| `roles.edit` | PUT | `/api/roles` | 角色管理 | ✓ | | |
| `roles.delete` | DELETE | `/api/roles` | 角色管理 | ✓ | | |
| `perms.list` | GET | `/api/permissions` | 权限管理 | ✓ | ✓ | |

## 认证失败处理 / Authorization Failure

```
用户无权限 → PermissionAuthorizationHandler.Fail()
           → ASP.NET Core 返回 HTTP 403
           → 响应体为空（由 JwtBearer 处理）
```

---

> **前端权限文档**：[frontend/docs/permissions.md](../frontend/docs/permissions.md) — 完整模块级权限码矩阵、v-permission 指令、路由守卫
