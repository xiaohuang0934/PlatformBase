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
| **Core** | `PlatformBase.Core` | Entity hierarchy, repository contracts, unified response, exceptions, `ICurrentUserService` |
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
| Audit Tracking / 审计 | `ICurrentUserService` → `AppDbContext` auto-fills `CreatedBy` / `UpdatedBy` / `DeletedBy` | [架构设计](docs/architecture.md) |
| Soft Delete / 软删除 | `SoftDeleteEntity` + global query filter auto-excludes deleted records | [数据库设计](docs/database.md) |
| Unified Response / 统一响应 | `ApiResult<T>` — all endpoints return `{ success, code, message, data, traceId }` | [架构设计](docs/architecture.md) |
| Multi-Database / 多数据库 | SQLite (default) / SQL Server / MySQL, config-driven switching | [数据库设计](docs/database.md) |
| Paging & Sorting / 分页排序 | `PagedRequest` + dynamic `OrderBy` via expression trees | [开发指南](docs/dev-guide.md) |
| Global Exception / 全局异常 | `GlobalExceptionMiddleware` catches `BusinessException` + unhandled | [架构设计](docs/architecture.md) |
| Redis Cache / 缓存 | Permission cache + login rate-limit, auto-degrade to DB | [权限管理](docs/permissions.md) |
| Structured Logging / 日志 | Serilog — Console + daily rolling file, 30-day retention | [架构设计](docs/architecture.md) |
| Health Check / 健康检查 | `GET /health` — DB connectivity check | [部署指南](docs/deployment.md) |
| Swagger JWT / 接口文档 | Bearer paste authorization flow, `POST /api/auth/login` | [开发指南](docs/dev-guide.md) |

---

## 数据库 / Database

6 tables, auto-created on first run with seed data:

| Table / 表 | Description / 说明 |
|-------------|---------------------|
| `Users` | User entity (`SoftDeleteEntity` + BCrypt hash + lockout) |
| `Roles` | Role definitions (permission groups) |
| `UserRoles` | M:N user-role mapping |
| `Permissions` | API endpoint definitions (Code + Path + Method) |
| `RolePermissions` | M:N role-permission mapping |
| `UserPermissions` | User direct permissions (with `IsGranted` override) |

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
| Validation | FluentValidation | Pipeline validation |

---

## 文档体系 / Documentation

| Document / 文档 | Content / 内容 |
|-----------------|-----------------|
| [架构设计](docs/architecture.md) | Architecture decisions, middleware pipeline, entity hierarchy |
| [认证授权](docs/auth.md) | JWT + IdentityServer4 + ICurrentUserService + audit |
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
- [x] `ICurrentUserService` session context injection
- [x] Login lockout + rate-limit (DB + Redis)

### 基础业务模块 / Basic Business Modules
- [ ] 系统参数 — Key-Value 配置中心，Redis 缓存 + 运行时修改无需重启
- [ ] 数据字典 — 类型/项两级结构，Redis 缓存，支持层级 + 按编码批量获取
- [ ] 操作日志 — 关键操作异步记录，分页检索，不阻塞请求
- [ ] 文件管理 — 统一上传/下载/预览，本地存储 + OSS 扩展点

### 进阶特性 / Advanced Features
- [ ] Background jobs (Hangfire)
- [ ] Multi-tenancy
- [ ] Event bus
- [ ] Distributed tracing (OpenTelemetry)
