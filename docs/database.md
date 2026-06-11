# 数据库设计 / Database Design

## 表清单 / Table Inventory

| 表 / Table | 来源 / Source | 主键 / PK | 说明 / Description |
|------------|--------------|-----------|---------------------|
| `Users` | 自建 | `Guid` | 用户实体（继承 SoftDeleteEntity） |
| `Roles` | 自建 | `Guid` | 角色定义（继承 AuditableEntity） |
| `UserRoles` | 自建 | 复合 (UserId, RoleId) | 用户-角色 M:N |
| `Permissions` | 自建 | `Guid` | API 权限定义 |
| `RolePermissions` | 自建 | 复合 (RoleId, PermissionId) | 角色-权限 M:N |
| `UserPermissions` | 自建 | 复合 (UserId, PermissionId) | 用户直达权限 |
| `SystemParams` | 自建 | `Guid` | 系统参数（运行时 K-V 配置） |
| `DataDictTypes` | 自建 | `Guid` | 数据字典类型 |
| `DataDictItems` | 自建 | `Guid` | 数据字典项（含层级 ParentId） |
| `JobSchedules` | 自建 | `Guid` | 任务调度配置 |
| `OperationLogs` | 自建 | `Guid` | 操作日志（继承 AuditableEntity） |
| `Tenants` | 自建 | `Guid` | 租户 |
| `PlatformUserTenants` | 自建 | 复合 | 平台账号-租户映射 |
| `TenantParams` | 自建 | `Guid` | 租户覆盖参数 |
| `FileAttachments` | 自建 | `Guid` | 文件附件 |
| `NotificationTemplates` | 自建 | `Guid` | 通知模板 |
| `Notifications` | 自建 | `Guid` | 通知记录 |
| `OrganizationUnits` | 自建 | `Guid` | 组织架构（物化路径） |
| `Menus` | 自建 | `Guid` | 菜单（树形+权限绑定） |

> **注意**：PlatformBase 不依赖 ASP.NET Core Identity，所有表均为自建实体。关联表（UserRoles / RolePermissions / UserPermissions）使用复合主键，不继承 BaseEntity 体系。

## 表关系图 / Entity Relationship Diagram

```
Users (SoftDeleteEntity)             Roles (AuditableEntity)       Permissions (BaseEntity)
  ├ Id (PK)                            ├ Id (PK)                     ├ Id (PK)
  ├ Username / NormalizedUsername      ├ Name / NormalizedName       ├ Code (UNIQUE)
  ├ Email / NormalizedEmail            ├ Description                 ├ Name
  ├ PasswordHash (BCrypt)              ├ CreatedAt/CreatedBy         ├ ResourcePath
  ├ SecurityStamp                      └ UpdatedAt/UpdatedBy         ├ HttpMethod
  ├ LockoutEnd / AccessFailedCount                                    ├ GroupName
  ├ IsActive                         UserRoles                        ├ SortOrder
  ├ CreatedAt/CreatedBy                ├ UserId (PK, FK→Users)       ├ IsEnabled
  ├ UpdatedAt/UpdatedBy                └ RoleId (PK, FK→Roles)       └ Description
  ├ IsDeleted / DeletedAt / DeletedBy
  └ ...                              RolePermissions
                                       ├ RoleId (PK, FK→Roles)
                                     └ PermissionId (PK, FK→Perms)

                                    UserPermissions
                                       ├ UserId (PK, FK→Users)
                                       ├ PermissionId (PK, FK→Perms)
                                       └ IsGranted (true=授权, false=拒绝)
```

## Users 表 / Users Table

| 列 / Column | 类型 / Type | 约束 / Constraint | 说明 |
|-------------|------------|-------------------|------|
| `Id` | `uniqueidentifier` | PK | |
| `Username` | `nvarchar(256)` | UNIQUE | 登录名 |
| `NormalizedUsername` | `nvarchar(256)` | INDEX | 大写，大小写不敏感查找 |
| `Email` | `nvarchar(256)` | NULLABLE | |
| `NormalizedEmail` | `nvarchar(256)` | INDEX | |
| `EmailConfirmed` | `bit` | DEFAULT 0 | |
| `PasswordHash` | `nvarchar(max)` | | BCrypt 哈希（算法+盐+哈希） |
| `SecurityStamp` | `nvarchar(max)` | | 密码变更时更新 |
| `PhoneNumber` | `nvarchar(max)` | NULLABLE | |
| `TwoFactorEnabled` | `bit` | DEFAULT 0 | 预留 |
| `LockoutEnd` | `datetimeoffset` | NULLABLE | 锁定到期时间 |
| `LockoutEnabled` | `bit` | DEFAULT 1 | |
| `AccessFailedCount` | `int` | DEFAULT 0 | |
| `IsActive` | `bit` | DEFAULT 1 | |
| `CreatedAt` | `datetime` | | 审计时间戳 |
| `CreatedBy` | `nvarchar(max)` | NULLABLE | 审计操作人 |
| `UpdatedAt` | `datetime` | NULLABLE | 审计时间戳 |
| `UpdatedBy` | `nvarchar(max)` | NULLABLE | 审计操作人 |
| `IsDeleted` | `bit` | DEFAULT 0 | 软删除标记 |
| `DeletedAt` | `datetime` | NULLABLE | 软删除时间 |
| `DeletedBy` | `nvarchar(max)` | NULLABLE | 删除操作人 |

## Roles 表 / Roles Table

| 列 / Column | 类型 / Type | 约束 / Constraint | 说明 |
|-------------|------------|-------------------|------|
| `Id` | `uniqueidentifier` | PK | |
| `Name` | `nvarchar(256)` | UNIQUE | Admin / Manager / User |
| `NormalizedName` | `nvarchar(256)` | INDEX | |
| `Description` | `nvarchar(max)` | NULLABLE | |
| `CreatedAt` / `CreatedBy` | | | 审计 |
| `UpdatedAt` / `UpdatedBy` | | | 审计 |

## Permissions 表 / Permissions Table

| 列 / Column | 类型 / Type | 约束 / Constraint | 说明 |
|-------------|------------|-------------------|------|
| `Id` | `uniqueidentifier` | PK | |
| `Code` | `nvarchar(100)` | UNIQUE | `users.create` |
| `Name` | `nvarchar(100)` | | "创建用户" |
| `ResourcePath` | `nvarchar(200)` | | `/api/users` |
| `HttpMethod` | `nvarchar(10)` | | GET / POST / PUT / DELETE |
| `GroupName` | `nvarchar(50)` | NULLABLE | 展示分组 |
| `SortOrder` | `int` | DEFAULT 0 | |
| `IsEnabled` | `bit` | DEFAULT 1 | |
| `Description` | `nvarchar(200)` | NULLABLE | |

## 关联表 / Junction Tables

| 表 | 主键 | 额外字段 |
|----|------|---------|
| `UserRoles` | (UserId FK→Users, RoleId FK→Roles) | — |
| `RolePermissions` | (RoleId FK→Roles, PermissionId FK→Permissions) | — |
| `UserPermissions` | (UserId FK→Users, PermissionId FK→Permissions) | `IsGranted` bit |

## 种子数据 / Seed Data

### 角色

| Name | Description |
|------|-------------|
| Admin | 系统管理员 — 拥有全部权限 |
| Manager | 业务管理员 — 用户和角色查看 |
| User | 普通用户 — 最小权限 |

### 权限

| Code | ResourcePath | HttpMethod |
|------|-------------|------------|
| `users.list` | `/api/users` | GET |
| `users.create` | `/api/users` | POST |
| `users.edit` | `/api/users` | PUT |
| `users.delete` | `/api/users` | DELETE |
| `roles.list` | `/api/roles` | GET |
| `roles.create` | `/api/roles` | POST |
| `roles.edit` | `/api/roles` | PUT |
| `roles.delete` | `/api/roles` | DELETE |
| `perms.list` | `/api/permissions` | GET |

### 角色-权限

| 角色 | 权限 |
|------|------|
| Admin | 全部 9 个 |
| Manager | `users.list`, `roles.list`, `perms.list` |
| User | `users.list` |

### 用户

| Username | Password | Email | 角色 |
|----------|----------|-------|------|
| admin | Admin@123 | admin@platformbase.com | Admin |
| testuser | Test@123 | testuser@platformbase.com | User |

## 数据库切换 / Database Provider Switching

```jsonc
// appsettings.json
"Database": {
  "Provider": "Sqlite",   // Sqlite | SqlServer | MySql
  "ConnectionString": "Data Source=app.db"
}
```

### SQL Server

```jsonc
"Database": {
  "Provider": "SqlServer",
  "ConnectionString": "Server=.;Database=PlatformBase;Trusted_Connection=true;TrustServerCertificate=true"
}
```

### MySQL

```jsonc
"Database": {
  "Provider": "MySql",
  "ConnectionString": "Server=localhost;Database=PlatformBase;User=root;Password=123456;"
}
```

首次启动时 `DataSeeder.SeedAsync` 调用 `EnsureCreatedAsync` 自动建表。生产环境应改用 EF Core Migration (`dotnet ef database update`)。

## 建库机制 / Database Initialization

```
应用启动
  ├─ DataSeeder.SeedAsync(WebApplication):
  │   ├─ EnsureCreatedAsync()  — 首次自动建表
  │   ├─ SeedRolesAsync()      — 幂等检查后插入
  │   ├─ SeedPermissionsAsync()
  │   ├─ SeedRolePermissionsAsync()
  │   └─ SeedUsersAsync()      — 幂等检查后插入
  └─ 应用就绪
```

所有种子操作均为幂等（已存在则跳过），多次启动不会产生重复数据。

## 后续扩展表 / Future Tables (v1.1)

### 系统参数表 / SystemParams

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `Id` | `uniqueidentifier` | PK | |
| `Code` | `nvarchar(100)` | UNIQUE INDEX | 参数编码（如 `site_name`） |
| `Name` | `nvarchar(100)` | | 参数名称 |
| `Value` | `nvarchar(max)` | | 参数值（统一 string 存储） |
| `Category` | `nvarchar(50)` | INDEX | 分类（general / security / feature-toggle） |
| `Description` | `nvarchar(500)` | NULLABLE | 说明 |
| `IsEnabled` | `bit` | DEFAULT 1 | |
| `SortOrder` | `int` | DEFAULT 0 | |
| (继承 SoftDeleteEntity) | | | 审计 + 软删除 |

> Redis 缓存 Key：`sysparam:{code}` / `sysparam:cat:{category}`，30 分钟过期

### 数据字典类型表 / DataDictTypes

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `Id` | `uniqueidentifier` | PK | |
| `TypeCode` | `nvarchar(100)` | UNIQUE INDEX | 类型编码（如 `gender`） |
| `TypeName` | `nvarchar(100)` | | 类型名称（如 "性别"） |
| `Description` | `nvarchar(500)` | NULLABLE | |
| `IsEnabled` | `bit` | DEFAULT 1 | |
| `SortOrder` | `int` | DEFAULT 0 | |
| (继承 SoftDeleteEntity) | | | 审计 + 软删除 |

### 数据字典项表 / DataDictItems

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `Id` | `uniqueidentifier` | PK | |
| `DictTypeId` | `uniqueidentifier` | FK→DataDictTypes | 所属类型 |
| `ItemCode` | `nvarchar(100)` | UNIQUE(DictTypeId, ItemCode) | 项编码 |
| `ItemName` | `nvarchar(100)` | | 项名称 |
| `ItemValue` | `nvarchar(200)` | NULLABLE | 扩展值 |
| `ParentId` | `uniqueidentifier` | NULLABLE FK→自身 | 父级项 |
| `IsEnabled` | `bit` | DEFAULT 1 | |
| `SortOrder` | `int` | DEFAULT 0 | |
| (继承 SoftDeleteEntity) | | | 审计 + 软删除 |

> Redis 缓存 Key：`dict:{typeCode}`，30 分钟过期

### 任务调度配置表 / JobSchedules

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `Id` | `uniqueidentifier` | PK | |
| `JobId` | `nvarchar(100)` | UNIQUE INDEX | 任务标识 |
| `JobName` | `nvarchar(100)` | | 任务名称 |
| `CronExpression` | `nvarchar(50)` | | Cron 表达式 |
| `IsEnabled` | `bit` | DEFAULT 1 | 启停状态 |
| `Description` | `nvarchar(500)` | NULLABLE | 说明 |
| `LastRunAt` | `datetime` | NULLABLE | 上次执行时间 |
| `LastError` | `nvarchar(max)` | NULLABLE | 错误信息 |
| (继承 AuditableEntity) | | | 审计（不用软删除） |

### 操作日志表 / OperationLogs (v1.2)

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `Id` | `uniqueidentifier` | PK | |
| `UserId` | `uniqueidentifier` | INDEX NULLABLE | 操作用户 |
| `Username` | `nvarchar(100)` | NULLABLE | 用户名快照 |
| `Action` | `nvarchar(50)` | INDEX | 操作类型（login/create/update/delete） |
| `Resource` | `nvarchar(200)` | | 操作资源（如 User:admin） |
| `Detail` | `nvarchar(max)` | NULLABLE | 变更详情 JSON |
| `IpAddress` | `nvarchar(50)` | NULLABLE | 客户端 IP |
| `UserAgent` | `nvarchar(500)` | NULLABLE | 浏览器标识 |
| `IsSuccess` | `bit` | DEFAULT 1 | 是否成功 |
| `Timestamp` | `datetime` | INDEX | 操作时间 |
| (继承 AuditableEntity) | | | 审计（日志只读，不支持软删除） |

> 写入方式：Hangfire `BackgroundJob.Enqueue` 异步入队，不阻塞 HTTP 请求。查询按 Timestamp 倒序。

### 租户表 / Tenants (v1.3)

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `Id` | `uniqueidentifier` | PK | |
| `Name` | `nvarchar(100)` | | 租户名称 |
| `Code` | `nvarchar(100)` | UNIQUE INDEX | 租户编码 |
| `ContactEmail` | `nvarchar(200)` | NULLABLE | 联系人邮箱 |
| `IsEnabled` | `bit` | DEFAULT 1 | |
| (继承 AuditableEntity) | | | 审计 |

### 组织架构表 / OrganizationUnits (v1.3)

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `Id` | `uniqueidentifier` | PK | |
| `TenantId` | `uniqueidentifier` | | 所属租户 |
| `Name` | `nvarchar(100)` | | 部门名称 |
| `Code` | `nvarchar(100)` | INDEX | 部门编码 |
| `Path` | `nvarchar(500)` | INDEX | 物化路径（如 /1/4/7/） |
| `ParentId` | `uniqueidentifier` | NULLABLE FK→自身 | 上级部门 |
| `SortOrder` | `int` | DEFAULT 0 | |
| `IsEnabled` | `bit` | DEFAULT 1 | |
| (继承 TenantSoftDeleteEntity) | | | 审计 + 软删除 + 租户 |

> 物化路径：`Path LIKE '/1/4/%'` 一次查询找到所有子部门

### 菜单表 / Menus (v1.3)

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `Id` | `uniqueidentifier` | PK | |
| `TenantId` | `uniqueidentifier` | | 所属租户 |
| `Type` | `int` | | 1=目录 2=页面 3=按钮 |
| `Name` | `nvarchar(100)` | | 显示名称 |
| `ParentId` | `uniqueidentifier` | NULLABLE INDEX | 父级菜单 |
| `Path` | `nvarchar(200)` | NULLABLE | 前端路由路径 |
| `Component` | `nvarchar(200)` | NULLABLE | 前端组件路径 |
| `Icon` | `nvarchar(50)` | NULLABLE | 图标 class |
| `PermissionCode` | `nvarchar(100)` | NULLABLE INDEX | 绑定权限编码 |
| `IsVisible` | `bit` | DEFAULT 1 | |
| `IsEnabled` | `bit` | DEFAULT 1 | |
| `KeepAlive` | `bit` | | 页面缓存 |
| (继承 TenantSoftDeleteEntity) | | | 审计 + 软删除 + 租户 |
