# PlatformBase / 平台底座

**Enterprise-grade .NET 8 WebAPI Starter** — 企业级 .NET 8 WebAPI 通用开发底座。提供 JWT 认证授权、RBAC 权限管理、Redis 缓存、审计追踪、软删除、多数据库、统一响应等开箱即用的基础设施。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-512BD4)](https://learn.microsoft.com/en-us/ef/core/)
[![IdentityServer4](https://img.shields.io/badge/IdentityServer4-OAuth2%2FOIDC-blue)](https://duendesoftware.com/)
[![Swagger](https://img.shields.io/badge/Swagger-OAS3-brightgreen)](https://swagger.io/)
[![Redis](https://img.shields.io/badge/Redis-cache-red)](https://redis.io/)
[![OpenCode](https://img.shields.io/badge/Built%20with-OpenCode-blue)](https://opencode.ai)
[![DeepSeek](https://img.shields.io/badge/DeepSeek-V4%20Pro-4B6BFB)](https://deepseek.ai)

---

## 架构 / Architecture

Clean Architecture (DDD 分层), dependency flows inward:

```
Host (Startup / Middleware / Controllers / IdentityServer4)
  └── Infrastructure (EF Core / Repository)
        └── Application (DTO / Service Interfaces)
              └── Core (Entity / Repository Contracts / Models)
                    ← zero external dependencies
```

| Layer / 层 | Project / 项目 | Responsibility / 职责 |
|-------------|----------------|------------------------|
| **Core** | `PlatformBase.Core` | Entity hierarchy, repository contracts, unified response, exceptions, `ICurrentUserContext` |
| **Application** | `PlatformBase.Application` | DTOs, service interfaces (`IUserService`, `IPermissionService`) |
| **Infrastructure** | `PlatformBase.Infrastructure` | EF Core `AppDbContext` (audit auto-fill), repository impl, unit of work |
| **Host** | `PlatformBase.Host` | Startup, IdentityServer4, JWT auth, RBAC authorization, Redis, Swagger |

> Detailed architecture decisions: [架构设计](docs/architecture.md)

---

## 快速开始 / Quick Start

### Prerequisites / 前置条件

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Optional: [Redis](https://redis.io/) (auto-degrades to DB if not running)

### Build & Run / 构建 & 启动

```bash
dotnet build
cd src/PlatformBase.Host && dotnet run   # default SQLite + port 5269
```

### Verify / 验证

```bash
curl http://localhost:5269/health                    # 200
open http://localhost:5269/swagger                   # Swagger UI
curl -X POST http://localhost:5269/api/auth/login \  # JWT Token
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}'
```

> Swagger: paste returned `accessToken` into **Authorize** 🔒 button → all APIs auto-attach `Authorization: Bearer`

---

## 核心特性 / Core Features

| Feature / 特性 | Description / 说明 | Details / 详情 |
|----------------|---------------------|----------------|
| JWT Auth / 认证 | IdentityServer4 + 自建 User 体系，X509 自签名证书，BCrypt 密码哈希 | [认证授权](docs/auth.md) |
| RBAC Permissions / 权限 | 角色继承 + 用户直达权限 + `IsGranted` 覆盖 + Redis 缓存降级 | [权限管理](docs/permissions.md) |
| Audit Tracking / 审计 | `ICurrentUserContext` → `AppDbContext` auto-fills `CreatedBy` / `UpdatedBy` / `DeletedBy` | [架构设计](docs/architecture.md) |
| Soft Delete / 软删除 | `SoftDeleteEntity` + global query filter auto-excludes deleted records | [数据库设计](docs/database.md) |
| Unified Response / 统一响应 | `ApiResult<T>` — all endpoints return `{ success, code, message, data, traceId }` | [架构设计](docs/architecture.md) |
| Multi-Database / 多数据库 | SQLite (default) / SQL Server / MySQL, config-driven switching | [数据库设计](docs/database.md) |
| Paging & Sorting / 分页排序 | `PagedRequest` + dynamic `OrderBy` via expression trees | [开发指南](docs/dev-guide.md) |
| Global Exception / 全局异常 | `GlobalExceptionMiddleware` catches `BusinessException` + unhandled | [架构设计](docs/architecture.md) |
| Redis Cache / 缓存 | Permission cache + login rate-limit, auto-degrade to DB | [权限管理](docs/permissions.md) |
| Structured Logging / 日志 | Serilog — Console + daily rolling file, 30-day retention | [架构设计](docs/architecture.md) |
| Health Check / 健康检查 | `GET /health` — DB connectivity check | [部署指南](docs/deployment.md) |
| Swagger JWT / 接口文档 | Bearer paste authorization flow, `POST /api/auth/login` | [开发指南](docs/dev-guide.md) |
| Multi-Tenancy / 多租户 | ITenantAware 全局过滤器 + 平台/租户双表方案 + 参数继承 | [架构设计](docs/architecture.md) |
| System Params / 系统参数 | Key-Value 配置中心，功能开关，Redis 缓存降级 | [开发指南](docs/dev-guide.md) |
| Data Dictionary / 数据字典 | 两级 Type/Item 结构 + 层级 ParentId + Redis 缓存 | [开发指南](docs/dev-guide.md) |
| Background Jobs / 定时任务 | Hangfire，API 启停/动态Cron/手动触发，Dashboard | [开发指南](docs/dev-guide.md) |
| Event Bus / 事件总线 | System.Threading.Channels，预留 RabbitMQ 切换 | [架构设计](docs/architecture.md) |
| Operation Log / 操作日志 | ActionFilter 自动记录，Hangfire 异步入队，分页检索 | [开发指南](docs/dev-guide.md) |
| File Management / 文件管理 | 统一上传/下载，本地存储 + OSS 可插拔 | [开发指南](docs/dev-guide.md) |
| Menu Management / 菜单管理 | 树形菜单 + PermissionCode 权限绑定 + 权限裁剪 | [开发指南](docs/dev-guide.md) |
| API Versioning / 版本管理 | UrlSegment 版本化 `v{version}` + Swagger 分组 | [开发指南](docs/dev-guide.md) |

---

## 数据库 / Database

22 tables, auto-created on first run with seed data:

| Table / 表 | Description / 说明 |
|-------------|---------------------|
| `Users` | User (SoftDeleteEntity + BCrypt + TenantId) |
| `Roles` | Role definitions (AuditableEntity + TenantId) |
| `UserRoles` | M:N user-role |
| `Permissions` | API permission definitions |
| `RolePermissions` | M:N role-permission |
| `UserPermissions` | User direct permissions (IsGranted) |
| `SystemParams` | System parameters (Key-Value) |
| `DataDictTypes` | Data dictionary types |
| `DataDictItems` | Data dictionary items (tree) |
| `OperationLogs` | Operation logs (async write) |
| `FileAttachments` | File storage metadata |
| `JobSchedules` | Background job schedules |
| `Tenants` | Multi-tenant |
| `PlatformUserTenants` | Platform user-tenant mapping |
| `TenantParams` | Tenant parameter overrides |
| `NotificationTemplates` | Notification templates |
| `Notifications` | User notifications |
| `OrganizationUnits` | Org tree (Materialized Path) |
| `Menus` | Menu tree + permission binding |

> Full schema: [数据库设计](docs/database.md)

---

## 配置 / Configuration

```jsonc
{
  "Database": { "Provider": "Sqlite", "ConnectionString": "Data Source=app.db" },
  "Jwt": { "Secret": "min-32-chars...", "Issuer": "http://localhost:5269" },
  "IdentityServer": { "Authority": "http://localhost:5269" },
  "Redis": { "ConnectionString": "localhost:6379", "Enabled": true },
  "Cors": { "AllowedOrigins": ["*"] }   // restrict in production
}
```

> Database switching, JWT config, production X509 certificates: [部署指南](docs/deployment.md)

---

## 技术栈 / Tech Stack

| Category / 类别 | Component / 组件 | Purpose / 用途 |
|-----------------|-------------------|----------------|
| Runtime | .NET 8 (LTS) | Long-term support |
| Web Framework | ASP.NET Core | Controller-based API |
| OAuth2/OIDC | IdentityServer4 | Token issuance (Password Grant) |
| JWT Auth | JwtBearer + X509 | Access token validation |
| Permissions | Custom RBAC | User/Role/Permission model |
| ORM | EF Core 8 | SQLite / SQL Server / MySQL |
| Password | BCrypt.Net-Next | Password hashing |
| Cache | StackExchange.Redis | Permission cache + rate-limit |
| Logging | Serilog | Console + daily rolling file |
| API Docs | Swashbuckle | OpenAPI 3.0 + Bearer JWT |
| Validation | FluentValidation | Pipeline validation + 密码复杂度 |
| Request Logging | Serilog RequestLogging | 全量请求记录(路径/方法/耗时/状态码) |
| Audit Snapshot | EF Core ChangeTracker | 数据变更前后快照自动捕获 |

---

## 文档体系 / Documentation

| Document / 文档 | Content / 内容 |
|-----------------|-----------------|
| [架构设计](docs/architecture.md) | Architecture decisions, middleware pipeline, entity hierarchy |
| [设计决策](docs/design-decisions.md) | 关键设计决策及备选方案对比 |
| [代码模式](docs/patterns.md) | 代码模式与小巧思（AppendIf, 缓存降级, 物化路径等） |
| [模块业务](docs/modules.md) | 各模块业务逻辑完整说明 |
| [API 参考](docs/api-reference.md) | 92 个端点全量清单（路由/方法/权限） |
| [错误码](docs/error-codes.md) | ErrorCode 完整体系及客户端处理建议 |
| [配置参考](docs/configuration.md) | appsettings.json 完整说明 + 数据库切换 |
| [认证授权](docs/auth.md) | JWT + IdentityServer4 + ICurrentUserContext + audit |
| [权限管理](docs/permissions.md) | RBAC model, `[Permission]` attribute, Redis cache |
| [数据库设计](docs/database.md) | Table schema, entity relationships, seed data |
| [开发指南](docs/dev-guide.md) | Add business modules, coding conventions |
| [部署指南](docs/deployment.md) | Production X509 cert, K8s, nginx, troubleshooting |
| [变更日志](docs/changelog.md) | Version history |

---

## 路线图 / Roadmap

### 基础设施 / Infrastructure
- [x] Unified response & global exception
- [x] Guid PK + typed ID
- [x] Audit tracking + soft delete (with `*By` auto-fill)
- [x] Multi-database (SQLite / SQL Server / MySQL)
- [x] Paging & dynamic sorting
- [x] Repository + unit of work
- [x] Structured logging (Serilog)
- [x] Health check
- [x] Swagger Bearer JWT integration

### 认证授权 / Authentication & Authorization
- [x] JWT auth (IdentityServer4 + custom User)
- [x] RBAC permissions (role inheritance + user override + Redis)
- [x] `ICurrentUserContext` session context injection
- [x] Login lockout + rate-limit (DB + Redis)

### 基础业务模块 / Basic Business Modules
- [x] 系统参数 — Key-Value + 功能开关 + SMTP 配置（支持多配置+租户覆盖）
- [x] 数据字典 — Type/Item 两级 + 树形 ParentId + Redis 缓存
- [x] 用户管理 — 完整 CRUD + FluentValidation 密码复杂度（大小写+数字+符号）
- [x] 角色管理 — CRUD + 权限全量替换 + 事务保护
- [x] 权限管理 — CRUD + 级联清理 + Code 匹配鉴权
- [x] 操作日志 — ActionFilter + Hangfire 异步入队 + ChangeTracker 审计快照
- [x] 消息通知 — 站内信 + 模板 + SMTP 邮件通道（支持多配置+租户覆盖）
- [x] 部门管理 — 树形 + 物化路径(Path LIKE 查询子部门)
- [x] 文件管理 — 上传/下载 + 本地存储 + OSS 预留
- [x] 数据导入导出 — Excel(ClosedXML) + CSV(CsvHelper) 自动识别
- [x] 菜单管理 — 树形 + PermissionCode 权限绑定 + 权限裁剪
- [x] 数据权限 — [DataScope] ActionFilter 预留框架

### 进阶特性 / Advanced Features
- [x] Background jobs (Hangfire) — API 启停/动态Cron/Dashboard
- [x] 事件总线 — Channel + IEventPublisher 预留 RabbitMQ
- [x] 多租户 — ITenantAware 全局过滤器 + AccessibleTenantIds + 平台/租户双表
- [x] 分布式锁(ID) + API版本 + 限流 + 国际化
- [x] 请求日志 — Serilog RequestLogging(全量/路径/耗时)
- [x] 审计日志增强 — ChangeTracker 数据变更快照
- [x] 告警通知 — SMTP 邮件通道(SystemParam 配置)
- [x] 优雅关闭 — IHostApplicationLifetime
- [ ] 客户端管理 — 多端 Client 注册（预留）
- [ ] 分布式追踪 (OpenTelemetry)
