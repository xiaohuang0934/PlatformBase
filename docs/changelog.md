# 变更日志 / Changelog

## v1.7 — 前端全模块 + 后端查询参数补全 + Bug 修复 (2026-06-13)

### 新增

- **前端全模块开发** — Vue3+TS 83 源文件，14 个业务模块完整 PC 端 + 移动端 CRUD
- **墨石 (Inkstone) 设计系统** — 暗/亮双模 120+ CSS 令牌，WCAG AA 对比度达标
- **移动端全模块** — 钻取式菜单、抽屉导航、卡片列表、FAB+action-sheet CRUD、van-list 滚动加载
- **Tabler Icons 迁移** — 18 个图标从 Element Plus 迁移至 @tabler/icons-vue
- **JWT 自动刷新** — axios 拦截器 + 请求队列防并发 + localStorage 持久化

### 修复

- **角色模块** — 新增 Code/IsSystem 字段，isSystem 筛选器从静默失效修复为可用
- **权限模块** — 创建表单补齐必填字段 ResourcePath/HttpMethod，更新请求移除不存在的 code 字段
- **系统参数** — 创建表单补齐 name 字段，删除操作增加确认弹窗
- **数据字典** — DesktopDataDictList.vue 脚本补全（修复 25 个缺失变量）
- **租户模块** — 新增 Description 字段 + keyword/IsEnabled 筛选 + sortField 排序
- **菜单模块** — GET /menus 新增 parentId 查询参数
- **操作日志** — Keyword 补全到 Username/Action/Detail 模糊搜索
- **数据字典** — Keyword 从 PagedRequest 移入 filter 表达式
- **服务健康** — CORS 配置 Already 支持 JWT 认证

### 后端查询参数完善

- `GET /roles` — 新增 `isSystem` 筛选
- `GET /menus` — 新增 `parentId` 筛选
- `GET /tenants` — 新增 `keyword`/`isEnabled`/`sortField`/`isAscending`
- `GET /operation-logs` — 新增 `keyword` 模糊搜索 Detail 字段
- `GET /data-dict/types` — 新增 `keyword` 模糊搜索 TypeName/TypeCode
- `POST /auth/login` — OperationLogFilter 从未认证请求体提取用户名
- `Global JsonNamingPolicy.CamelCase` — 所有 API 响应统一为 camelCase

## v1.6 — 安全加固 + 可观测性 + 告警通知 (2026-06-11)

### 新增

- **请求日志 (Serilog RequestLogging)**
  - `app.UseSerilogRequestLogging()` — 每个 HTTP 请求记录路径/方法/耗时/状态码/TraceId
  - 零代码改动，仅 3 行配置

- **FluentValidation 密码复杂度**
  - `CreateUserValidator` — 密码复杂度校验（大写+小写+数字+特殊字符 四选四）+ 用户名长度
  - `ChangePasswordValidator` — 跨字段校验（新密码 ≠ 当前密码）
  - `ResetPasswordValidator` — 密码强度校验
  - `AddFluentValidationAutoValidation()` 替换 DataAnnotations 方式

- **审计日志增强 — ChangeTracker 快照**
  - `AppDbContext.CaptureChangeSnapshot()` — 捕获所有 Modified 实体的 Old/New 值
  - `OperationLogFilter` 自动合并快照到 Detail 字段
  - 过滤 Key/ForeignKey/Navigation 属性，避免循环引用

- **告警通知 SMTP 增强**
  - `SmtpChannelProvider` — SMTP 邮件通道
  - 配置从 `appsettings.json` → `SystemParam(smtp:default)`，运行时修改无需重启
  - 支持多 SMTP 配置（smtp:alert / smtp:marketing / smtp:default）
  - 支持租户覆盖（TenantParam Fallback）
  - `SmtpConfig` DTO 用于 JSON 反序列化
  - 修复 Singleton/Scoped 生命周期冲突（IServiceProvider 懒解析）

- **优雅关闭**
  - `IHostApplicationLifetime` — SIGTERM 时等待 10s 处理 Hangfire 任务
  - `Program.cs` 末尾注册

- **部署指南增强**
  - `docs/deployment.md` — 顶部新增 ⚠️ Migration 必用警告（生产环境 `EnsureCreated` ≠ `Migration`）

### 变更

- `OperationLogFilter` 注入 `AppDbContext`，读取 `ChangeSnapshot` 属性
- `SmtpChannelProvider` 从 `IConfiguration` → `ISystemParamService`（IServiceProvider）
- 健康检查注释增加扩展指引（Redis/Disk 需 NuGet 包）

---

## v1.5 — 多租户可见性完善 + 全模块补齐 + 代码优雅化 (2026-06-10)

### 新增

- **多租户可见性控制**
  - `ICurrentUserService.AccessibleTenantIds` — 控制读取可见范围
  - 平台管理员 → 查询 `PlatformUserTenants` 表获取已分配租户列表
  - 租户用户 → 仅可见自己租户数据
  - `CurrentUserService.LoadAssignedTenants()` — 懒加载已分配租户

- **AppDbContext 自动填充 TenantId**
  - `ApplyAuditFields` 增加 `ITenantAware` 实体自动填充 `TenantId`
  - 全局过滤器改为 `AccessibleTenantIds.Contains(e.TenantId)` 模式

- **Service 层补齐**
  - `IOrganizationUnitService` + `OrganizationUnitService` — 组织架构 Service 层
  - `ITenantService` + `TenantService` — 租户管理 Service 层
  - 平台账号-租户分配端点：`POST/DELETE /api/v1/tenants/{tid}/platform-users/{uid}`

- **通知模板 CRUD**
  - `GET/POST/PUT/DELETE /api/v1/notifications/templates`
  - `notifications.manage` 权限

- **操作日志详情 + 清理**
  - `GET /api/v1/operation-logs/{id}` — 单条详情
  - `DELETE /api/v1/operation-logs/cleanup?daysAgo=90` — 批量清理

- **ExpressionExtensions.Append/AppendIf** — 链式表达式追加，5 个 Service GetPagedAsync 全面简化

### 变更

- `ExpressionExtensions` — +`Append` / `AppendIf` 两个方法
- 5 个 Service 的 `GetPagedAsync` 重写为链式 `AppendIf`，消除 ~105 行冗余代码
- 删除 3 个 `CombineAnd` 私有方法（已由 `AndAlso` + `Append` 替代）
- `UserController.Create` — 自动填充 `TenantId` + `UserType`
- `RoleService.CreateAsync` — 自动填充 `TenantId`
- `RoleService.GetPagedAsync` — 增加租户过滤
- `UserService.GetPagedAsync` — 增加租户过滤（注入 `ICurrentUserService`）
- `Program.cs` — 合并分散的手动注册为统一注释块，移除重复 using
- 移除未使用的 `FluentValidation.AspNetCore` NuGet 包

### 修复

- 重复 `using PlatformBase.Core.Events;` × 2 → 移除
- `Normalize` 方法统一使用 `StringExtensions.Normalize`
- 6 个 `ITenantAware` 实体创建时 `TenantId` 从 `Guid.Empty` → 自动填充
- 全局过滤器：TenantId=null 不再等于"看全部" → 改为 `AccessibleTenantIds` 精确控制

---

## v1.3 — 多租户 + 事件总线 + 文件管理 + 消息通知 + 导入导出 + 部门管理 (2026-06-10)

### 新增 / Added

- **多租户基础设施**
  - `ITenantAware` 接口 + `TenantAuditableEntity` / `TenantSoftDeleteEntity` 基类
  - `Tenants` / `PlatformUserTenants` 表 — 租户 + 平台账号映射
  - `TenantParams` 表 — 租户参数覆盖（双表方案，Fallback 到 SystemParams）
  - `TenantDataDictTypes` / `TenantDataDictItems` — 租户字典覆盖
  - 全局查询过滤器 `ITenantAware` — `AppDbContext` 自动附加 `WHERE TenantId = {current}`
  - JWT Claims 扩展 `tenant_id` + `user_type`
  - `ICurrentUserService` 扩展 +`TenantId` + `IsSuperAdmin` + X-Tenant-Id 头切换
  - `GET/POST/PUT/DELETE /api/v1/tenants` — 租户管理 CRUD
  - `GET/POST/PUT /api/v1/tenant-params` — 租户参数 CRUD
  - 种子数据：默认租户 ×1、platform_admin ×1、tenant_user ×1

- **事件总线**
  - `IEventPublisher` / `IEventHandler<T>` 接口（Core 层抽象）
  - `ChannelEventBus` — System.Threading.Channels 内存实现（预留 RabbitMQ 切换）
  - `EventBusExtensions` — 自动扫描注册所有 Handler

- **文件管理**
  - `FileAttachments` 表（Bucket/Key/OriginalName/Size/MimeType/BizType/BizId）
  - `IFileStorageProvider` 抽象 — `LocalFileStorageProvider` 本地实现
  - `POST /api/v1/files/upload` — multipart/form-data 上传
  - `GET /api/v1/files/{id}/download` — 流式下载
  - `GET /api/v1/files?bizType=&bizId=` — 业务检索
  - `DELETE /api/v1/files/{id}` — 软删除

- **消息通知**
  - `NotificationTemplates` / `Notifications` 表
  - `INotificationService` — 模板发送 + 直接发送 + 分页查询 + 已读标记
  - `IChannelProvider` 抽象 — `InAppChannelProvider` 站内信实现（预留邮件/短信）
  - `GET /api/v1/notifications?unreadOnly=` — 通知列表
  - `PATCH /api/v1/notifications/{id}/read` — 标记已读
  - `PATCH /api/v1/notifications/read-all` — 全部已读
  - 种子模板：welcome / password_changed / account_locked

- **数据导入导出**
  - `IExportService` / `IImportService` — Excel/CSV 通用接口（ClosedXML + CsvHelper）
  - `GET /api/v1/import-export/users` — 导出用户列表为 .xlsx
  - `POST /api/v1/import-export/users` — 导入用户（Excel/CSV 自动识别）

- **部门管理**
  - `OrganizationUnits` 表（树形 ParentId）
  - `GET /api/v1/organization-units` — 树形列表
  - `POST/PUT/DELETE` — CRUD 管理

- **分布式基础能力**
  - `ILockService` + `RedisLockService` — 分布式锁
  - `IIdGenerator` + `GuidIdGenerator` — 分布式 ID（预留 Snowflake）

- **API 版本 + 限流 + 国际化**
  - `Asp.Versioning.Mvc` — 所有 Controller 路由加 `v{version:apiVersion}` 段
  - `[RateLimit(limit, seconds)]` ActionFilter — Redis 滑动窗口
  - `IStringLocalizer<SharedResource>` + zh-CN/en 资源文件 — AuthService 错误消息多语言

### 新增表 / New Tables

| 表 | 说明 |
|----|------|
| `Tenants` | 租户 |
| `PlatformUserTenants` | 平台账号-租户映射 |
| `TenantParams` | 租户覆盖参数 |
| `TenantDataDictTypes` | 租户覆盖字典类型 |
| `TenantDataDictItems` | 租户覆盖字典项 |
| `FileAttachments` | 文件附件 |
| `NotificationTemplates` | 通知模板 |
| `Notifications` | 通知记录 |
| `OrganizationUnits` | 组织架构 |

### 新增权限

| 编码 | 说明 |
|------|------|
| `tenants.*` | 租户管理（4 个） |
| `tenant-params.*` | 租户参数管理（4 个） |
| `files.upload` | 文件管理（1 个） |
| `org-units.*` | 组织架构管理（4 个） |

### 变更 / Changed

- 现有 4 张表改基类为 `TenantXxxEntity`：OperationLog / FileAttachment / Notification / NotificationTemplate
- `AppDbContext` — 新增全局 TenantId 过滤器
- `ICurrentUserService` — 新增 `TenantId` + `IsSuperAdmin`
- `AuthService` / `ResourceOwnerPasswordValidator` — JWT Claims 加入 `tenant_id` + `user_type`
- 8 个 Controller 路由 + `[ApiVersion]` + `v{version}` 段
- README 路线图全面更新

---

## v1.2 — 用户/角色/权限全生命周期 + 操作日志 (2026-06-10)

### 新增 / Added

- **用户管理完整 CRUD**
  - `UserController` — `GET/POST/PUT/DELETE/PATCH /api/users`
  - 分页列表 / 详情（含角色）/ 创建（含角色分配）/ 更新 / 软删除
  - `PATCH /api/users/{id}/toggle` — 启用/禁用
  - `POST /api/users/{id}/reset-password` — 管理员重置密码
  - `PUT /api/users/{id}/roles` — 批量分配角色（全量替换 + 事务保护）
  - `GetPagedAsync` / `SetActiveAsync` / `SoftDeleteAsync` / `ResetPasswordAsync` / `ClearRolesAsync`

- **角色管理完整 CRUD**
  - `RoleController` — `GET/POST/PUT/DELETE /api/roles`
  - 分页列表 / 详情 / 创建 / 更新 / 删除（含用户关联保护）
  - `GET /api/roles/{id}/permissions` — 查看角色权限
  - `PUT /api/roles/{id}/permissions` — 批量分配权限（全量替换 + 事务保护）

- **权限管理完整 CRUD**
  - `PermissionController` — `GET/POST/PUT/DELETE /api/permissions`
  - 分页列表 / 详情 / 创建 / 更新 / 删除（级联清理关联）
  - 权限拆分：`perms.create` / `perms.edit` / `perms.delete`（不再共用 `perms.list`）

- **操作日志模块**
  - `OperationLogs` 表 — 记录关键操作（登录/创建/更新/删除），异步写入不阻塞请求
  - `[OperationLog("create", Resource = "User")]` — 标记特性，一行即可记录
  - `OperationLogFilter` — 全局 `IAsyncActionFilter`，通过 Hangfire `BackgroundJob.Enqueue` 异步入队
  - `GET /api/operation-logs` — 分页检索，支持按用户/操作类型/时间范围筛选

- **DTO 输入校验**
  - 全部 7 个 Create DTO + `ChangePasswordDto` 添加 `[Required]` / `[MinLength]` 等 DataAnnotations
  - `[ApiController]` 自动触发 ModelState 校验，返回 400 + 中文错误提示

- **统一响应约定**
  - `JwtBearerEvents.OnChallenge` / `OnForbidden` — 统一返回 `ApiResult(code=401)`
  - `StampValidationMiddleware` — 统一返回 `ApiResult(code=401, message="Token已失效")`
  - `GlobalExceptionMiddleware` — `DbUpdateConcurrencyException` 返回 409，HTTP 统一 200
  - `PermissionAuthorizationHandler` — try-catch 防护，异常时 `context.Fail()`

- **缓存安全加固**
  - 登录频控 — Redis 计数器（5 分钟窗口 / 10 次上限），Redis 不可用时降级到 DB 锁定
  - 权限缓存失效 — 角色权限变更 / 用户角色变更 / 权限点变更 → 立即失效 `user:perms:{userId}`
  - 密码变更撤销 Token — 改密码/重置密码后主动删除 `stamp:{userId}` + 撤销 RefreshToken
  - 权限缓存 TTL 从 30min 缩短为 5min

- **事务保护**
  - 用户创建（创建+角色分配）、用户角色分配（清除+新增）、角色权限分配（移除+新增）
  - 密码修改（改密码+撤销Token）、密码重置（改密码+撤销Token）
  - 全部 5 处关键操作包裹 `IUnitOfWork.BeginTransactionAsync/CommitTransactionAsync`

- **消除重复代码**
  - `Core/Extensions/StringExtensions.Normalize()` — 统一字符串规范化
  - `Core/Extensions/ExpressionExtensions.AndAlso<T>()` — 统一 Lambda 表达式合并

- **错误码清理**
  - 移除未使用的 `PermissionDenied(1010)`（与 `Forbidden(403)` 语义重复）
  - `GlobalExceptionMiddleware` 对 `DbUpdateConcurrencyException` 返回 409

### 新增表 / New Tables

| 表 | 说明 |
|----|------|
| `OperationLogs` | 操作日志（异步写入） |

### 新增 API 端点

| 前缀 | 数量 | 关键端点 |
|------|:---:|------|
| `/api/users` | 9 | CRUD + toggle + roles + reset-password |
| `/api/roles` | 7 | CRUD + permissions |
| `/api/permissions` | 5 | CRUD（perms.create/edit/delete 独立权限） |
| `/api/operation-logs` | 1 | GET 分页检索 |

### 新增权限

| 编码 | 说明 |
|------|------|
| `perms.create/edit/delete` | 权限管理 CRUD（3 个） |
| `operation-logs.list` | 操作日志查询（1 个） |

### 变更 / Changed

- `ErrorCode.cs` — 移除未使用的 `PermissionDenied(1010)`，规范注释
- `Program.cs` — JwtBearer OnChallenge/OnForbidden 返回统一 ApiResult
- `ResetPasswordRequest` — 从 UserController.cs 移至 DTOs 目录
- `OperationLogFilter` — 全局注册，无需手动在每个 Controller 中添加

---

## v1.1 — 基础业务模块 (2026-06-09)

### 新增 / Added

- **系统参数模块**
  - `SystemParams` 表 — 运行时 Key-Value 配置中心
  - `ISystemParamService` — 参数读取（带 Redis 缓存）、CRUD 管理
  - 功能开关支持 — `Category="feature-toggle"` + `IsFeatureEnabledAsync`
  - `GET /api/system-params/{code}` — 公开接口，无需登录
  - `GET /api/system-params/features` — 功能开关列表
  - 种子数据：9 个默认参数（站点名称/分页大小/登录锁定/Token有效期/功能开关等）

- **数据字典模块**
  - `DataDictTypes` + `DataDictItems` 两级表结构，Item 支持层级（ParentId 自引用）
  - `IDataDictService` — 字典 CRUD + 快查接口（带 Redis 缓存，树形组装）
  - `GET /api/data-dict/code/{typeCode}` — 公开接口，前端下拉框使用
  - `GET /api/data-dict/codes?codes=a&codes=b` — 批量获取，减少网络往返
  - 种子数据：gender / user_status / enabled_status 三组字典

- **后台任务调度（Hangfire）**
  - `JobSchedules` 表 — 任务配置持久化（Cron + 启停状态）
  - `IBackgroundJobService` — 启停/动态Cron/手动触发/状态查询
  - `POST /api/jobs/{jobId}/start|stop` — 通过 API 启停任务
  - `PUT /api/jobs/{jobId}/cron` — 动态修改执行计划
  - Hangfire Dashboard（`/hangfire`，Admin 角色可访问）
  - 启动时自动从 JobSchedules 表同步配置到 Hangfire
  - `CleanupExpiredTokensJob` — 示例任务：每天凌晨 3 点清理过期 Token
  - 存储适配：SQL Server → SqlServerStorage，其余 → InMemoryStorage

- **DI 注册优化**
  - `AddApplicationServices()` — 程序集扫描自动注册 `I*Service` → `*Service`
  - 新增业务模块无需修改 `Program.cs`

### 变更 / Changed

- `AuthController` 重构：BCrypt/Token 签发逻辑下沉到 `AuthService`
- `Program.cs` 服务注册从 3 行手动 → 1 行自动扫描
- README 路线图新增"基础业务模块"分类并标记完成
- `docs/dev-guide.md` Step 4 更新为自动注册说明

### 新增表 / New Tables
| 表 | 说明 |
|----|------|
| `SystemParams` | 系统参数（Key-Value 配置） |
| `DataDictTypes` | 数据字典类型 |
| `DataDictItems` | 数据字典项（含层级） |
| `JobSchedules` | 任务调度配置 |

### 新增权限 / New Permissions
| 编码 | 说明 |
|------|------|
| `system-params.*` | 系统参数管理（4 个） |
| `datadict.*` | 数据字典管理（4 个） |
| `jobs.list` / `jobs.manage` | 任务调度管理（2 个） |

---

## v1.0 — 认证授权 & 权限管理 (2026-06-09)

### 新增 / Added

- **JWT 认证授权体系**
  - 自建 User / Role 实体体系（不依赖 ASP.NET Core Identity）
  - IdentityServer4 嵌入式 OAuth2 Token 服务（Password Grant）
  - JwtBearer Token 验证（共享 X509 签名密钥，零 HTTP 发现调用）
  - `POST /api/auth/login` — Swagger 可用的登录端点
  - `GET /api/auth/profile` — 当前用户资料
  - `POST /api/auth/change-password` — 修改密码
  - BCrypt.Net-Next 密码哈希存储
  - 登录失败锁定（5 次/5 分钟）
  - Redis 辅助登录频控（IP + 用户名维度）

- **RBAC 权限管理**
  - 6 张表：`Users` / `Roles` / `UserRoles` / `Permissions` / `RolePermissions` / `UserPermissions`
  - 角色继承 + 用户直达权限 + `IsGranted` 覆盖
  - `[Permission("code")]` 标记属性 + `PermissionAuthorizationHandler`
  - `GET /api/auth/permissions` — 前端权限清单接口
  - Redis 权限缓存（30 min）+ 自动降级

- **用户会话上下文注入**
  - `ICurrentUserService` — 注入到 Service / DbContext
  - `AppDbContext.ApplyAuditFields` 自动填充 `CreatedBy` / `UpdatedBy` / `DeletedBy`

- **Swagger JWT 集成**
  - Bearer Token 粘贴方案（`ApiKey` 类型 + `AddSecurityRequirement`）
  - Swashbuckle 6.9.0（修复了 10.x OpenApi 安全序列化 bug）

- **种子数据**
  - 幂等种子：3 个角色 / 9 个权限 / 2 个用户
  - 启动时自动 `EnsureCreated` 建表 + 插入种子

### 变更 / Changed

- `AppDbContext` 构造注入 `ICurrentUserService`，`ApplyAuditFields` 增强
- `ErrorCode` 新增 7 个认证权限错误码（1004-1010）
- `Program.cs` 完整注册链：IdentityServer4 → JwtBearer → 权限鉴权 → Redis → Swagger Bearer

### 技术选型 / Technical Decisions

| 决策 | 理由 |
|------|------|
| 自建 User 而非 ASP.NET Core Identity | 保持 AppDbContext 为 DbContext（非 IdentityDbContext），User 继承 SoftDeleteEntity 与业务实体体系一致 |
| X509 自签名证书而非 RSA.Create() | macOS Apple Security Framework RSA 与 JWT 签名/验签不兼容 |
| Swashbuckle 6.9.0 而非 10.x | 10.x 的 OpenApi 2.x `OpenApiSecurityRequirement` 序列化 bug，始终输出空安全声明 |
| `ApiKey` 类型 + explicit Header name | `SecuritySchemeType.Http` 在 Swagger UI 中不自动附加 Authorization header |

---

## v0.1 — 初始版本 (2026-06-08)

> **构建方式：** 本版本由 **OpenCode** + **DeepSeek V4 Pro** 从零开始构建，全程 AI 辅助编码，人工审核把关。

### 新增 / Added

- **Clean Architecture 四层项目结构** — Core / Application / Infrastructure / Host
- **实体继承体系** — `BaseEntity<TKey>` → `BaseEntity` → `AuditableEntity` → `SoftDeleteEntity`
- **基础设施**
  - `IRepository<T>` + `IKeyedRepository<T, TKey>` 通用仓储契约
  - `IUnitOfWork` 工作单元 + 事务支持
  - `EfRepository<T>` EF Core 实现（分页 + 动态排序）
  - `AppDbContext` 软删除全局过滤 + 审计时间戳
- **统一 API 响应** — `ApiResult<T>` / `ApiResult`
- **全局异常处理** — `GlobalExceptionMiddleware`
- **BusinessException** 业务异常 + `ErrorCode` 错误码体系
- **多数据库支持** — SQLite (默认) / SQL Server / MySQL
- **结构化日志** — Serilog Console + 文件按天滚动
- **健康检查** — `GET /health`
- **Swagger API 文档** + FluentValidation 管道校验
- **CORS** 跨域配置
- **DDD 强类型 ID** — `TypedId<T>` 值对象基类
