# 变更日志 / Changelog

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
