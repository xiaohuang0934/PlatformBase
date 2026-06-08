# PlatformBase

**企业级 .NET 8 WebAPI 通用开发底座** —— 提供统一响应、审计追踪、软删除、多数据库、全局异常处理等开箱即用的基础设施，业务模块只需关注领域逻辑。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-512BD4)](https://learn.microsoft.com/en-us/ef/core/)
[![Serilog](https://img.shields.io/badge/Serilog-structured-green)](https://serilog.net/)
[![Swagger](https://img.shields.io/badge/Swagger-OAS3-brightgreen)](https://swagger.io/)

---

## 架构

采用 **Clean Architecture**（DDD 分层），依赖方向单向内聚：

```
Host (启动 / 配置 / 中间件)
  └── Infrastructure (EF Core / 仓储实现)
        └── Application (DTO / 应用服务接口)
              └── Core (实体 / 异常 / 仓储契约 / 通用模型)
                    ← 无外部依赖
```

| 层 | 项目 | 职责 |
|---|------|------|
| **Core** | `PlatformBase.Core` | 实体基类、仓储契约、统一响应模型、异常定义、值对象基类 |
| **Application** | `PlatformBase.Application` | DTO 基类、应用服务接口 |
| **Infrastructure** | `PlatformBase.Infrastructure` | EF Core DbContext、仓储实现、工作单元、DI 注册扩展 |
| **Host** | `PlatformBase.Host` | ASP.NET Core 启动、中间件、Controller、配置文件 |

---

## 项目结构

```
PlatformBase/
├── PlatformBase.sln
└── src/
    ├── PlatformBase.Core/
    │   ├── DatabaseProvider.cs              # 数据库类型枚举 (Sqlite/SqlServer/MySql)
    │   ├── Primitives/
    │   │   └── TypedId.cs                   # DDD 强类型 ID 值对象基类
    │   ├── Entities/
    │   │   └── BaseEntity.cs                # IEntity / BaseEntity<TKey> / AuditableEntity / SoftDeleteEntity
    │   ├── Exceptions/
    │   │   ├── BusinessException.cs          # 业务异常（含错误码）
    │   │   └── ErrorCode.cs                 # 错误码常量定义
    │   ├── Models/
    │   │   ├── ApiResult.cs                 # 统一 API 响应体
    │   │   ├── HealthReportModel.cs          # 健康检查报告模型
    │   │   └── PagedResult.cs               # 分页请求 / 响应
    │   └── Repositories/
    │       ├── IRepository.cs               # 仓储契约 (Guid + 泛型主键)
    │       └── IUnitOfWork.cs               # 工作单元契约（事务支持）
    ├── PlatformBase.Application/
    │   ├── Dtos/
    │   │   └── BaseDto.cs                   # DTO 基类
    │   └── Services/
    │       └── ICrudService.cs              # 通用 CRUD 服务接口
    ├── PlatformBase.Infrastructure/
    │   ├── Data/
    │   │   ├── AppDbContext.cs              # EF Core DbContext（软删除过滤 + 审计自动填充）
    │   │   └── UnitOfWork.cs                # 工作单元实现
    │   ├── Extensions/
    │   │   └── ServiceCollectionExtensions.cs  # AddDatabase() DI 注册扩展
    │   └── Repositories/
    │       └── EfRepository.cs              # EF Core 仓储实现（分页 + 动态排序）
    └── PlatformBase.Host/
        ├── Controllers/
        │   └── HealthController.cs           # 健康检查端点 (GET /health)
        ├── Middleware/
        │   └── GlobalExceptionMiddleware.cs   # 全局异常转 ApiResult
        ├── Program.cs                        # 启动入口
        ├── appsettings.json                  # 基础配置（含注释）
        └── appsettings.Development.json      # 开发环境覆盖配置
```

---

## 快速开始

### 前置条件

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### 构建 & 运行

```bash
# 构建
dotnet build

# 启动（默认 SQLite + 端口 5269）
cd src/PlatformBase.Host && dotnet run
```

### 验证

```bash
# 健康检查
curl http://localhost:5269/health

# Swagger 文档 (仅开发环境)
open http://localhost:5269/swagger
```

### 健康检查响应示例

```json
{
  "success": true,
  "code": 200,
  "message": "success",
  "data": {
    "status": "Healthy",
    "duration": "00:00:00.0002970",
    "entries": []
  },
  "traceId": null
}
```

---

## 核心特性

### 1. 统一 API 响应

所有接口返回 `ApiResult<T>`，格式固定：

```json
{
  "success": true,
  "code": 200,
  "message": "success",
  "data": { ... },
  "traceId": null
}
```

```csharp
// 成功
return ApiResult<UserDto>.Ok(user);
return ApiResult<UserDto>.Fail(ErrorCode.DataNotFound, "用户不存在");
return ApiResult<>Fail("参数有误");

// 业务异常（由中间件自动捕获）
throw new BusinessException("用户名已存在", ErrorCode.DuplicateRecord);
```

### 2. 全局异常处理

`GlobalExceptionMiddleware` 作为管道最前端，统一捕获两类异常：

| 异常类型 | 处理方式 |
|----------|----------|
| `BusinessException` | Warning 日志 → `ApiResult.Fail(code, message)` |
| 未处理异常 | Error 日志 → 开发环境返回堆栈，生产环境返回 `"Internal server error"` |

### 3. Guid 主键

- 默认 `BaseEntity` = `BaseEntity<Guid>`
- 保留泛型 `BaseEntity<TKey>` 扩展点（支持 long、int 等）
- 可选 DDD 强类型 ID：`public record UserId : TypedId<Guid>`

### 4. 实体审计

实现 `IAuditable` 或继承 `AuditableEntity`，`DbContext` 自动填充时间戳：

| 字段 | 触发时机 |
|------|----------|
| `CreatedAt` | EntityState.Added |
| `UpdatedAt` | EntityState.Modified |

### 5. 软删除

继承 `SoftDeleteEntity`，获得软删除能力：

| 字段 | 说明 |
|------|------|
| `IsDeleted` | 软删除标记，全局查询过滤器自动排除 |
| `DeletedAt` | 删除时自动记录 UTC 时间 |
| `DeletedBy` | 删除人（待认证模块接入后自动填充） |

```csharp
uow.Repository<User>().SoftDelete(user);
await uow.SaveChangesAsync();
```

### 6. 实体继承体系

```
BaseEntity<TKey>            ← 泛型主键
  └─ BaseEntity (Guid)      ← 默认主键
       ├─ AuditableEntity    ← + CreatedAt / UpdatedAt / CreatedBy / UpdatedBy
       │    └─ SoftDeleteEntity  ← + IsDeleted / DeletedAt / DeletedBy
       └─ BaseEntity          ← 纯 ID，无审计
```

### 7. 分页查询

```csharp
var request = new PagedRequest
{
    PageIndex = 1,
    PageSize = 20,
    SortField = "CreatedAt",
    IsAscending = false
};

var result = await uow.Repository<User>().GetPagedAsync(request);
// result.TotalCount / result.TotalPages / result.Items
```

### 8. 仓储 + 工作单元

```csharp
public class UserService : ICrudService<User, UserDto, CreateUserDto, UpdateUserDto>
{
    private readonly IUnitOfWork _uow;

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct)
    {
        var user = new User { Name = dto.Name };
        await _uow.Repository<User>().AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        return MapToDto(user);
    }
}
```

---

## 配置说明

### 数据库切换

修改 `appsettings.json` 中的 `Database:Provider` 和 `ConnectionString`：

```jsonc
// SQLite (默认，开发)
"Database": {
  "Provider": "Sqlite",
  "ConnectionString": "Data Source=app.db"
}

// SQL Server
"Database": {
  "Provider": "SqlServer",
  "ConnectionString": "Server=.;Database=PlatformBase;Trusted_Connection=true;TrustServerCertificate=true"
}

// MySQL
"Database": {
  "Provider": "MySql",
  "ConnectionString": "Server=localhost;Database=PlatformBase;User=root;Password=123456;"
}
```

### 日志

Serilog 输出策略（在 `appsettings.json` 中配置）：

- **Console**：控制台实时输出
- **File**：按天滚动写入 `logs/` 目录，保留最近 30 天

### CORS

生产环境应将 `AllowedOrigins` 改为具体域名，禁止使用 `*`。

---

## 如何开始新业务模块

```csharp
// 1. Core 层 —— 定义实体
public class Product : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// 2. Application 层 —— 定义 DTO
public class ProductDto : BaseDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// 3. Application 层 —— 定义服务
public interface IProductService : ICrudService<Product, ProductDto, CreateProductDto, UpdateProductDto> { }

// 4. Host 层 —— 写 Controller
[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IProductService _service;

    [HttpGet]
    public async Task<ApiResult<PagedResult<ProductDto>>> Get([FromQuery] PagedRequest request)
    {
        var result = await _service.GetPagedAsync(request);
        return ApiResult<PagedResult<ProductDto>>.Ok(result);
    }
}
```

---

## 技术栈

| 类别 | 组件 | 说明 |
|------|------|------|
| 运行时 | .NET 8 (LTS) | 长期支持版本 |
| Web 框架 | ASP.NET Core | Controller-based API |
| ORM | EF Core 8 | SQLite / SQL Server / MySQL |
| 日志 | Serilog | Console + 文件按天滚动 |
| API 文档 | Swashbuckle | OpenAPI 3.0 |
| 参数校验 | FluentValidation | 管道自动校验 |

---

## 路线图

- [x] 统一响应 & 全局异常处理
- [x] Guid 主键 + 强类型 ID
- [x] 审计追踪 + 软删除
- [x] 多数据库支持
- [x] 分页 & 动态排序
- [x] 仓储 + 工作单元
- [x] 结构化日志
- [x] 健康检查
- [ ] JWT 认证 & 授权
- [ ] 分布式缓存 (Redis)
- [ ] 后台任务调度 (Hangfire)
- [ ] 多租户支持
- [ ] 事件总线
- [ ] 分布式追踪 (OpenTelemetry)
