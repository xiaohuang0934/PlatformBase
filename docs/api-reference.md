# API 端点全量参考 / API Reference

> 基础路径：`http://localhost:5269/api/v1`
> 所有响应统一格式：`{ success, code, message, data, traceId }`，HTTP 200

## 认证授权 (Auth)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `POST` | `/auth/login` | AllowAnonymous | 登录，返回 AT + RT |
| `POST` | `/auth/refresh` | AllowAnonymous | 刷新 Token |
| `GET` | `/auth/profile` | Authorize | 当前用户资料 |
| `POST` | `/auth/change-password` | Authorize | 修改密码 |
| `GET` | `/auth/permissions` | Authorize | 当前用户权限编码列表 |

## 用户管理 (Users)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/users?keyword=&isActive=&tenantIds=` | `users.list` | 分页列表（支持多租户查询） |
| `GET` | `/users/{id}` | `users.list` | 详情（含角色 + 部门列表） |
| `POST` | `/users` | `users.create` | 创建（必填：角色IDs + 部门IDs） |
| `PUT` | `/users/{id}` | `users.edit` | 更新（支持 UserType + 部门） |
| `DELETE` | `/users/{id}` | `users.delete` | 软删除 |
| `PATCH` | `/users/{id}/toggle` | `users.edit` | 启用/禁用 |
| `POST` | `/users/{id}/reset-password` | `users.edit` | 管理员重置密码 |
| `GET` | `/users/{id}/roles` | `users.list` | 查看角色 |
| `PUT` | `/users/{id}/roles` | `users.edit` | 分配角色（全量替换） |
| `GET` | `/users/{id}/organizations` | `users.list` | 查看部门 |
| `PUT` | `/users/{id}/organizations` | `users.edit` | 分配部门（全量替换） |
| `POST` | `/users/{id}/organizations/{orgId}` | `users.edit` | 添加用户到部门 |
| `DELETE` | `/users/{id}/organizations/{orgId}` | `users.edit` | 将用户移出部门 |

### 创建用户请求体 (CreateUserDto)

```json
{
  "username": "zhangsan",
  "password": "Pass@123",
  "email": "zhangsan@example.com",
  "phoneNumber": "13800138000",
  "tenantId": "guid",             // 平台管理员必填，租户管理员忽略
  "userType": 2,                  // 1=PlatformAdmin / 2=TenantAdmin / 3=TenantUser
  "roleIds": ["guid1", "guid2"],  // 必填
  "organizationUnitIds": ["guid"] // 必填
}
```

### 用户详情响应 (UserDto)

```json
{
  "id": "guid",
  "username": "zhangsan",
  "email": "zhangsan@example.com",
  "emailConfirmed": false,
  "phoneNumber": "13800138000",
  "isActive": true,
  "userType": 3,
  "roles": ["Admin"],
  "organizationUnits": [
    { "id": "guid", "name": "技术部", "code": "tech", "parentId": null, "sortOrder": 1 }
  ],
  "createdAt": "2026-01-01T00:00:00Z",
  "updatedAt": null
}
```

## 角色管理 (Roles)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/roles?keyword=&isSystem=&tenantIds=` | `roles.list` | 分页列表（支持多租户查询） |
| `GET` | `/roles/{id}` | `roles.list` | 详情（含 code/isSystem） |
| `POST` | `/roles` | `roles.create` | 创建（支持指定 TenantId 创建全局/租户级角色） |
| `PUT` | `/roles/{id}` | `roles.edit` | 更新 |
| `DELETE` | `/roles/{id}` | `roles.delete` | 删除（有关联用户则拒绝） |
| `GET` | `/roles/{id}/permissions` | `roles.list` | 查看权限编码列表 |
| `PUT` | `/roles/{id}/permissions` | `roles.edit` | 分配权限（全量替换） |

### 创建角色请求体 (CreateRoleDto)

```json
{
  "name": "运营经理",
  "code": "operation_manager",
  "description": "运营部门经理角色",
  "tenantId": null              // null=全局角色 / guid=租户级角色
}
```

## 权限管理 (Permissions)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/permissions` | `perms.list` | 分页列表（GroupName, ResourcePath, IsEnabled） |
| `GET` | `/permissions/{id}` | `perms.list` | 详情 |
| `POST` | `/permissions` | `perms.create` | 创建 |
| `PUT` | `/permissions/{id}` | `perms.edit` | 更新 |
| `DELETE` | `/permissions/{id}` | `perms.delete` | 删除（级联清理关联） |

## 系统参数 (System Params)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/system-params/{code}` | `system-params.list` | 按编码取值 |
| `GET` | `/system-params/category/{category}` | `system-params.list` | 按分类取值 |
| `GET` | `/system-params/features` | `system-params.list` | 功能开关列表 |
| `GET` | `/system-params` | `system-params.list` | 分页列表（Category, IsEnabled） |
| `GET` | `/system-params/detail/{id}` | `system-params.list` | 详情 |
| `POST` | `/system-params` | `system-params.create` | 创建 |
| `PUT` | `/system-params/{id}` | `系统-params.edit` | 更新 |
| `DELETE` | `/系统-params/{id}` | `系统-params.delete` | 软删除 |

## 租户参数 (Tenant Params)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/tenant-params/{code}` | `tenant-params.list` | 按编码查询当前租户参数 |
| `POST` | `/tenant-params` | `tenant-params.create` | 创建租户覆盖参数 |
| `PUT` | `/tenant-params/{id}` | `tenant-params.edit` | 更新 |

## 数据字典 (Data Dict)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/data-dict/types` | `datadict.list` | 类型分页列表 |
| `GET` | `/data-dict/types/{id}` | `datadict.list` | 类型详情 |
| `POST` | `/data-dict/types` | `datadict.create` | 创建类型 |
| `PUT` | `/data-dict/types/{id}` | `datadict.edit` | 更新类型 |
| `DELETE` | `/data-dict/types/{id}` | `datadict.delete` | 删除类型（级联软删除项） |
| `GET` | `/data-dict/types/{dictTypeId}/items` | `datadict.list` | 类型下项列表（树形） |
| `GET` | `/data-dict/items/{id}` | `datadict.list` | 项详情 |
| `POST` | `/data-dict/items` | `datadict.create` | 创建项 |
| `PUT` | `/data-dict/items/{id}` | `datadict.edit` | 更新项 |
| `DELETE` | `/data-dict/items/{id}` | `datadict.delete` | 删除项（软删除） |
| `GET` | `/data-dict/code/{typeCode}` | AllowAnonymous | 按编码获取字典项（公开） |
| `GET` | `/data-dict/codes?codes=a&codes=b` | AllowAnonymous | 批量获取字典项（公开） |

## 操作日志 (Operation Logs)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/operation-logs` | `operation-logs.list` | 分页列表（userId, action, username, time） |
| `GET` | `/operation-logs/{id}` | `operation-logs.list` | 详情 |
| `DELETE` | `/operation-logs/cleanup?daysAgo=90` | `operation-logs.list` | 清理 N 天前日志 |

## 菜单管理 (Menus)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/menus/tree` | Authorize | 当前用户可访问的菜单树（自动裁剪） |
| `GET` | `/menus` | `menus.list` | 全部菜单列表（可选 `?parentId=` 按父级筛选） |
| `GET` | `/menus/{id}` | `menus.list` | 详情 |
| `POST` | `/menus` | `menus.create` | 创建（校验父菜单租户归属） |
| `PUT` | `/menus/{id}` | `menus.edit` | 更新 |
| `DELETE` | `/menus/{id}` | `menus.delete` | 软删除 |

## 组织架构 (Organization Units)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/organization-units` | `org-units.list` | 树形列表 |
| `GET` | `/organization-units/{id}` | `org-units.list` | 详情（含 Path） |
| `POST` | `/organization-units` | `org-units.create` | 创建（自动计算物化路径） |
| `PUT` | `/organization-units/{id}` | `org-units.edit` | 更新 |
| `DELETE` | `/organization-units/{id}` | `org-units.delete` | 软删除 |
| `GET` | `/organization-units/{id}/users` | `org-units.list` | 查询部门下的用户（仅本部门） |
| `GET` | `/organization-units/{id}/users/with-children?keyword=&isActive=&pageIndex=&pageSize=` | `org-units.list` | 分页查询部门及子级的所有用户 |

## 租户管理 (Tenants)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/tenants?keyword=&isEnabled=&sortField=&isAscending=` | `tenants.list` | 分页列表（支持 keyword/IsEnabled 筛选 + 排序） |
| `GET` | `/tenants/{id}` | `tenants.list` | 详情 |
| `POST` | `/tenants` | `tenants.create` | 创建（含 Description） |
| `PUT` | `/tenants/{id}` | `tenants.edit` | 更新 |
| `DELETE` | `/tenants/{id}` | `tenants.delete` | 停用 |
| `GET` | `/tenants/accessible` | Authorize | 当前平台用户可访问的租户列表 |
| `GET` | `/tenants/{tid}/platform-users` | `tenants.edit` | 查看租户的平台账号 |
| `POST` | `/tenants/{tid}/platform-users/{uid}` | `tenants.edit` | 分配平台账号到租户 |
| `DELETE` | `/tenants/{tid}/platform-users/{uid}` | `tenants.edit` | 移除平台账号 |

## 消息通知 (Notifications)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/notifications?unreadOnly=` | Authorize | 用户通知列表 + 未读数 |
| `PATCH` | `/notifications/{id}/read` | Authorize | 标记已读 |
| `PATCH` | `/notifications/read-all` | Authorize | 全部已读 |
| `GET` | `/notifications/templates` | `notifications.manage` | 模板列表 |
| `POST` | `/notifications/templates` | `notifications.manage` | 创建模板 |
| `PUT` | `/notifications/templates/{id}` | `notifications.manage` | 更新模板 |
| `DELETE` | `/notifications/templates/{id}` | `notifications.manage` | 停用模板 |

## 文件管理 (Files)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/files?bizType=&bizId=` | `files.upload` | 按业务关联查询 |
| `POST` | `/files/upload` | `files.upload` | 上传（multipart/form-data） |
| `GET` | `/files/{id}/download` | `files.upload` | 下载（流式） |
| `DELETE` | `/files/{id}` | `files.upload` | 软删除 |

## 导入导出 (Import/Export)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/import-export/users?keyword=&isActive=&tenantId=` | `users.list` | 导出用户 Excel（支持按租户过滤） |
| `POST` | `/import-export/users?tenantId=` | `users.create` | 导入用户 Excel/CSV（平台管理员需指定租户） |

## 定时任务 (Jobs)

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/jobs` | `jobs.list` | 全部任务状态 |
| `GET` | `/jobs/{jobId}` | `jobs.list` | 单个任务详情 |
| `POST` | `/jobs/{jobId}/start` | `jobs.manage` | 启动 |
| `POST` | `/jobs/{jobId}/stop` | `jobs.manage` | 停止 |
| `PUT` | `/jobs/{jobId}/cron` | `jobs.manage` | 修改 Cron |
| `POST` | `/jobs/{jobId}/trigger` | `jobs.manage` | 手动触发 |
| `POST` | `/jobs/{jobId}/enqueue` | `jobs.manage` | 立即入队 |

## 健康检查

| 方法 | 端点 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/health` | — | 健康检查 |

---

### 端点统计

| 分类 | 数量 |
|------|:---:|
| 认证授权 | 5 |
| 用户管理 | 13 |
| 角色管理 | 7 |
| 权限管理 | 5 |
| 系统参数 | 8 |
| 租户参数 | 3 |
| 数据字典 | 12 |
| 操作日志 | 3 |
| 菜单管理 | 6 |
| 组织架构 | 7 |
| 租户管理 | 9 |
| 消息通知 | 7 |
| 文件管理 | 4 |
| 导入导出 | 2 |
| 定时任务 | 7 |
| 健康检查 | 1 |
| **合计** | **99** |
