# 模块业务逻辑 / Module Business Logic

## 用户管理 (User)

**Entity：** `User : SoftDeleteEntity`（非 `ITenantAware`，TenantId 手动处理）

**用户类型：** `UserType` 枚举 — 1=PlatformAdmin / 2=TenantAdmin / 3=TenantUser

| 端点 | 方法 | 说明 |
|------|------|------|
| `GET /api/v1/users?tenantIds=` | 分页列表 | 平台用户可指定租户列表；租户用户只看自己租户 |
| `GET /api/v1/users/{id}` | 详情 | 含角色列表 + 部门树节点（OrganizationUnits） |
| `POST /api/v1/users` | 创建 | 必填：roleIds + organizationUnitIds。经 DataScopeAuthorizationService 集中校验 |
| `PUT /api/v1/users/{id}` | 更新 | Email/Phone/UserType/OrganizationUnitIds |
| `DELETE /api/v1/users/{id}` | 删除 | 软删除 + 清理 UserRole/UserPermission/UserOrganizationUnit |
| `PATCH .../toggle` | 启用/禁用 | 切换 IsActive |
| `POST .../reset-password` | 密码重置 | 修改stamp + 撤销RT + 失效stamp缓存 |
| `GET .../roles` | 角色查询 | |
| `PUT .../roles` | 角色分配 | 全量替换（Clear + Add），事务保护 |
| `GET .../organizations` | 部门查询 | 查看用户所属部门列表 |
| `PUT .../organizations` | 部门分配 | 全量替换 + 事务保护 |
| `POST .../organizations/{orgId}` | 添加到部门 | 用户-部门 M:N 关系的独立添加 |
| `DELETE .../organizations/{orgId}` | 移出部门 | 用户-部门 M:N 关系的独立移除 |

**创建用户权限校验链（DataScopeAuthorizationService）：**
```
① 检查请求者用户类型是否在禁止列表 → 是则直接拒绝
② 根据用户层级确定目标租户和用户类型：
   - 平台管理员 → 必须指定租户（校验 TenantIds 范围），可指定创建 TenantAdmin/TenantUser
   - 租户管理员 → 强制当前租户，只能创建 TenantUser
③ 校验部门是否属于目标租户
④ 校验角色是全局角色或属于目标租户
```

**用户-部门多对多：** 通过 `UserOrganizationUnit` 关联表（复合主键 `UserId + OrganizationUnitId`）支持用户归属于多个部门。

---

## 角色管理 (Role)

**Entity：** `Role : AuditableEntity`（非 `ITenantAware`，TenantId 手动处理）

**角色层级：**
- `TenantId=null` → 全局角色，对所有租户可见
- `TenantId=guid` → 租户级角色，仅对该租户可见

| 端点 | 说明 |
|------|------|
| `GET /api/v1/roles?isSystem=&tenantIds=` | 分页列表（平台用户支持多租户查询） |
| `POST /api/v1/roles` | 创建 — 平台管理员可指定 TenantId 创建全局/租户级角色；租户管理员强制当前租户 |
| `PUT /api/v1/roles/{id}` | 更新 |
| `DELETE /api/v1/roles/{id}` | 删除 — 检查是否有用户关联 → 有则拒绝 |
| `GET .../permissions` | 查看角色权限编码列表 |
| `PUT .../permissions` | 分配权限 — 全量替换 + 事务 + 失效受影响用户缓存 |

**权限变更联动：** 角色权限变更后 → `InvalidateAffectedUsersCacheAsync` 失效所有拥有该角色的用户的 `user:perms:{userId}` 缓存。

---

## 权限管理 (Permission)

**Entity：** `Permission : BaseEntity`（全局共享，所有租户共用同一套 API 权限点）

**鉴权链路：**
```
[Permission("users.list")] → PolicyName="Permission:users.list"
  → PermissionPolicyProvider → 创建 PermissionRequirement
  → PermissionAuthorizationHandler
    → IsSuperAdmin? → 直接放行（绕过所有权限检查）
    → Redis GET "user:perms:{userId}" (5min TTL)
    → 未命中 → DB 合并：角色权限 + 用户直达权限（IsGranted 覆盖）
```

**CRUD 端点：** `GET/POST/PUT/DELETE /api/v1/permissions`

**权限变更：** 修改/删除权限点 → 5min TTL 内自然失效，不立即失效所有用户（批量失效成本高）。

---

## 系统参数 (SystemParam) + 租户参数 (TenantParam)

**双表架构：**
```
GetValueAsync(code, tenantId):
  ① TenantParams WHERE (TenantId, Code) → 租户覆盖值
  ② 未命中 → SystemParams WHERE Code → 全局默认值
  ③ Redis 缓存：sysparam:{tid}:{code} / sysparam:global:{code}
```

**SystemParam API：** `GET/POST/PUT/DELETE /api/v1/system-params`（平台管理员操作全局参数）

**TenantParam API：** `GET/POST/PUT /api/v1/tenant-params`（租户/平台管理员操作租户覆盖参数）

**功能开关：** `Category="feature-toggle"` + `Value="true"/"false"`，通过 `GetAllFeaturesAsync` 批量查询。

**内置参数：**
- `org_null_data_visibility` — 未归属部门数据的可见性（all / admin_only），由 DataScopeFilter 读取

---

## 数据字典 (DataDict)

**Entity：** `DataDictType : SoftDeleteEntity` + `DataDictItem : SoftDeleteEntity`（全局默认）

**双表扩展：** `TenantDataDictTypes` + `TenantDataDictItems`（租户覆盖，暂无 API）

**层级支持：** `DataDictItem.ParentId` 自引用 → 查询时组装 `Children` 树形结构。

**缓存：** `dict:{typeCode}` → 30min TTL，变更时精确失效。

**快查端点（公开）：**
- `GET /api/v1/data-dict/code/{typeCode}` — 单类型
- `GET /api/v1/data-dict/codes?codes=a&codes=b` — 批量

---

## 操作日志 (OperationLog)

**Entity：** `OperationLog : TenantAuditableEntity`

**写入链路：** `[OperationLog("create")]` 标记 → `OperationLogFilter`(IAsyncActionFilter) → `Hangfire.BackgroundJob.Enqueue` → `OperationLogWriterJob.WriteAsync` → DB

**HTTP 响应不阻塞：** ActionFilter 中 Enqueue 后立即返回。

**查询：**
- `GET /api/v1/operation-logs` — 分页（按时间/用户/操作类型筛选）
- `GET /api/v1/operation-logs/{id}` — 详情
- `DELETE /api/v1/operation-logs/cleanup?daysAgo=90` — 清理

**审计快照（v1.6）：** `AppDbContext.CaptureChangeSnapshot()` 自动捕获所有数据修改的 Before/After 值，合并到 OperationLog.Detail 字段。例如修改角色描述时：`{"Entity":"Role","Changes":{"Description":{"Old":"old desc","New":"new desc"}}}`

---

## 定时任务 (JobSchedule)

**Entity：** `JobSchedule : AuditableEntity`

**架构：** `JobRegistry` 注册可用任务 → 启动时从 `JobSchedules` 表同步到 Hangfire。

**管理 API：** `GET/POST start/stop/trigger/enqueue + PUT cron`

**Dashboard：** `/hangfire`（`HangfireAuthFilter` — Admin 角色可访问）

**存储适配：** SQL Server → SqlServerStorage；其余 → MemoryStorage

---

## 事件总线 (EventBus)

**接口（Core 层）：**
- `IEvent` — 事件标记接口
- `IEventPublisher.PublishAsync<T>(T @event)` — 发布
- `IEventHandler<T>.HandleAsync(T @event)` — 消费

**实现：** `ChannelEventBus` — `System.Threading.Channels` 内存队列

**自动发现：** `EventBusExtensions` 扫描所有 `IEventHandler<T>` 实现并注册。

**RabbitMQ 切换：** 只需实现 `IEventPublisher` / `IEventHandler<T>` 的新版本，其余代码零改动。

**当前状态：** 基础设施就绪，无实际事件使用（YAGNI — 等待业务需求驱动）。

---

## 文件管理 (FileAttachment)

**Entity：** `FileAttachment : TenantSoftDeleteEntity`

**存储抽象：** `IFileStorageProvider` — `LocalFileStorageProvider` 本地实现（可替换 OSS）

**API：**
- `POST /api/v1/files/upload` — multipart/form-data
- `GET /api/v1/files/{id}/download` — 流式输出
- `GET /api/v1/files?bizType=&bizId=` — 业务检索
- `DELETE /api/v1/files/{id}` — 软删除

---

## 消息通知 (Notification)

**双实体：** `NotificationTemplate`（模板）+ `Notification`（通知实例）

**发送链路：**
`INotificationService.SendByTemplateAsync(userId, templateCode, variables)`
  → 查模板 → `{变量}` 替换 → 写入 Notifications 表 → `IChannelProvider.SendAsync`（InApp + SMTP 邮件）

**通道（v1.6）：**
- `InAppChannelProvider` — 站内信（默认）
- `SmtpChannelProvider` — SMTP 邮件，配置存储在 SystemParam(smtp:default)，支持多配置(smtp:alert/smtp:marketing) + 租户覆盖
- 短信/企业微信 — 预留接口

**API：**
- `GET /api/v1/notifications` — 当前用户通知列表 + 未读数
- `PATCH .../{id}/read` — 标记已读
- `PATCH .../read-all` — 全部已读
- `GET /api/v1/notifications/templates` — 模板管理（CRUD）

**种子模板：** `welcome` / `password_changed` / `account_locked`

---

## 菜单管理 (Menu)

**Entity：** `Menu : TenantSoftDeleteEntity`，`PermissionCode` 绑定 RBAC 权限点

**三种类型：** 1=目录（侧边栏分组）、2=页面（路由映射）、3=按钮（权限点绑定）

**安全约束：** 创建子菜单时校验父菜单租户归属，禁止跨租户嵌套。

**平台管理员简化：** `UserType == PlatformAdmin` 时菜单不裁剪，返回全部。

**API：**
- `GET /api/v1/menus/tree` — 当前用户可访问的菜单树（自动裁剪无权限节点）
- `GET /api/v1/menus` — 全部菜单（管理）
- `POST/PUT/DELETE` — CRUD

**种子数据：** 18 条菜单 + 按钮，默认分组：
```
用户管理 → 用户列表 / 新建用户 / 编辑用户 / 删除用户
角色管理 → 角色列表 / 新建角色
权限管理 → 权限列表
系统管理 → 系统参数 / 数据字典 / 操作日志 / 定时任务 / 文件管理
组织架构 → 部门列表
系统配置 → 菜单管理
多租户 → 租户列表
```

---

## 组织架构 (OrganizationUnit)

**Entity：** `OrganizationUnit : TenantSoftDeleteEntity`，物化路径 `Path`

**树形管理：**
- `GET /api/v1/organization-units` — 树形列表
- `POST /api/v1/organization-units` — 创建（自动计算 Path）
- `PUT /api/v1/organization-units/{id}` — 更新
- `DELETE /api/v1/organization-units/{id}` — 软删除

**Path 计算：** 创建时读取父级 Path → `{parentPath}{newId}/` → 回写

**部门用户查询：**
- `GET /api/v1/organization-units/{id}/users` — 查询部门下的用户（仅本部门）
- `GET /api/v1/organization-units/{id}/users/with-children` — 分页查询部门及子级的所有用户

**数据权限（v1.8 完整实现）：**
`DataScopeFilter` 在 Action 执行前构建 `DataScopeFilterContext`，注入 `HttpContext.Items["DataScopeContext"]`：

```
① 检查 [DataScope] Attribute — 无标记则跳过
② IsSuperAdmin → 不过滤（CanViewAllTenant=true, IsSuperAdmin=true）
③ 平台管理员/租户管理员 → CanViewAllTenant=true，查看全租户数据
④ 普通用户 → 读取 org_null_data_visibility 参数 + AccessibleOrgPaths
   - "all" → 租户内全部可见
   - "admin_only" → 不可见未归属部门的数据
   - AccessibleOrgPaths → LIKE '{path}%' 限制部门及子级数据
```

---

## 多租户 (Tenant)

**核心表：** `Tenants` + `PlatformUserTenants` + `TenantParams`

**实体分类：**
```
ITenantAware（全局过滤器自动生效）：
  FileAttachment、OrganizationUnit、OperationLog、Notification、
  NotificationTemplate、Menu、TenantParam、TenantDataDictType/Item

IDataScopeAware（Department+Tenant 过滤）：
  继承 DataScopeEntity 的实体

非 ITenantAware（手动过滤）：
  User（TenantId nullable）、Role（TenantId nullable）

全局实体（不过滤）：
  SystemParam、DataDict、Permission、JobSchedule
```

**ICurrentUserContext 租户属性（v1.8 重设计）：**

| 属性 | 租户用户 | 平台用户 |
|------|---------|---------|
| `TenantId` | User.TenantId | null |
| `TenantIds` | [] | PlatformUserTenants 分配列表 |
| `CurrentTenantId` | TenantId | SetCurrentTenant 设置值 |
| `CurrentTenantIds` | [TenantId] | SetCurrentTenants 设置值 / [CurrentTenantId] / TenantIds |

**可见性规则（通过 CurrentTenantIds 控制）：**
- 租户用户 → `CurrentTenantIds = [TenantId]` → 只能看自己租户数据
- 平台用户(无 SetCurrentTenant) → `CurrentTenantIds = TenantIds` → 可看所有已分配租户
- 平台用户(有 SetCurrentTenant) → `CurrentTenantIds = [选中的]`
- `CurrentTenantIds.Count == 0` → 什么也看不到（安全默认）

**写入规则：**
- ITenantAware → `ApplyAuditFields` 自动填充 `TenantId`
- User → Controller 层经 `DataScopeAuthorizationService` 确定 `TenantId`
- Role → 同上

**Redis 缓存：**
| Key | TTL | 说明 |
|-----|-----|------|
| `user:tenant:{userId}` | 30min | 用户租户信息（TenantId + TenantIds） |

**API：**
- `GET/POST/PUT/DELETE /api/v1/tenants` — 租户 CRUD
- `GET /api/v1/tenants/accessible` — 当前平台用户可访问租户列表
- `POST/DELETE /api/v1/tenants/{tid}/platform-users/{uid}` — 平台账号分配（自动失效缓存）
- `GET/POST/PUT /api/v1/tenant-params` — 租户参数

---

## 分布式锁 + ID 生成

**ILockService：** Redis `StringSetAsync(key, token, When.NotExists, expiry)` — 尝试获取锁，Redis 不可用时返回 true

**IIdGenerator：** `GuidIdGenerator` — 默认 Guid 实现，预留 Snowflake 替换

---

## 导入导出

**接口：** `IExportService.ExportExcelAsync<T>(data, columns)` + `IImportService.ImportExcelAsync<T>(stream, columns, validator)`

**实现：** `ImportExportService` — ClosedXML（Excel）+ CsvHelper（CSV，Excel 失败自动降级）

**当前覆盖：** 用户列表的导出/导入。平台管理员需指定 tenantId 参数。导入经 `DataScopeAuthorizationService` 权限校验（禁止 TenantUser 导入）。

**API：**
- `GET /api/v1/import-export/users?keyword=&isActive=&tenantId=` — 导出
- `POST /api/v1/import-export/users?tenantId=` — 导入（multipart/form-data）
