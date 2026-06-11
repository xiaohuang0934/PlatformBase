# 开发指南 / Developer Guide

## 如何新增业务模块 / Adding a New Business Module

### Step 1: Core 层 — 定义实体 / Define Entity

```csharp
// src/PlatformBase.Core/Entities/Product.cs  （注：Entity 层不分模块）
public class Product : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
}
```

| 继承选择 | 适用场景 |
|---------|---------|
| `BaseEntity` | 纯主键，无需审计 |
| `AuditableEntity` | 需要创建/修改时间追踪 |
| `SoftDeleteEntity` | 需要软删除 + 审计追踪（推荐） |

### Step 2: Application 层 — 定义 DTO + Service 接口

```csharp
// src/PlatformBase.Application/Dtos/ProductModule/ProductDto.cs （注：按 Module 子目录组织）
public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// src/PlatformBase.Application/Dtos/ProductModule/CreateProductDto.cs
public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// src/PlatformBase.Application/Services/ProductModule/IProductService.cs （注：按 Module 子目录）
public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPagedAsync(PagedRequest request, CancellationToken ct);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct);
    Task UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

### Step 3: Host 层 — 实现 Service + Controller

```csharp
// src/PlatformBase.Host/Services/ProductService.cs
public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct)
    {
        var product = new Product { Name = dto.Name, Price = dto.Price };
        await _uow.Repository<Product>().AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);
        // AppDbContext 自动填充 CreatedAt / CreatedBy
        return new ProductDto { Id = product.Id, Name = product.Name, Price = product.Price };
    }
}

// src/PlatformBase.Host/Controllers/ProductController.cs
[ApiController, Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IProductService _service;

    [HttpGet]
    [Permission("products.list")]
    public async Task<ApiResult<PagedResult<ProductDto>>> Get([FromQuery] PagedRequest request)
    {
        var result = await _service.GetPagedAsync(request);
        return ApiResult<PagedResult<ProductDto>>.Ok(result);
    }

    [HttpPost]
    [Permission("products.create")]
    public async Task<ApiResult<ProductDto>> Create([FromBody] CreateProductDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return ApiResult<ProductDto>.Ok(result);
    }
}
```

### Step 4: 注册服务 / Register Service

**无需手动注册。** 只要遵循命名约定（接口 `IFooService` 在 Application 层，实现 `FooService` 在 Host 层），程序集扫描会自动完成注册：

```csharp
// Program.cs 中的 .AddApplicationServices() 会自动发现并注册 IProductService → ProductService
// 规则：Application 层 I*Service 接口 → Host 层同名 *Service 实现 → 统一 AddScoped
```

**特殊注册**（无接口模式、Core 层接口）仍需手动处理：
```csharp
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); // Core 层接口
builder.Services.AddScoped<PersistedGrantStore>(); // 无接口
```

### Step 5: 添加种子权限 / Add Seed Permission

```csharp
// DataSeeder.cs - GetSeedPermissions()
new Permission { Code = "products.list", Name = "产品列表", ResourcePath = "/api/products", HttpMethod = "GET" },
new Permission { Code = "products.create", Name = "创建产品", ResourcePath = "/api/products", HttpMethod = "POST" },
```

## 编码约定 / Coding Conventions

### 仓储使用 / Repository Usage

```csharp
// ✅ 正确：BaseEntity 实体通过 IUnitOfWork.Repository<T>
var user = await _uow.Repository<User>().GetByIdAsync(id, ct);
var roles = await _uow.Repository<Role>().FindAsync(r => r.IsEnabled, ct);
```

```csharp
// ✅ 正确：关联表（复合主键）通过 AppDbContext.Set<T>()
var userRoles = await _context.Set<UserRole>()
    .Where(ur => ur.UserId == userId)
    .ToListAsync(ct);
```

### 审计字段 / Audit Fields

```csharp
// ✅ 不需要手动填 *By 字段！AppDbContext 自动处理
await _uow.Repository<Product>().AddAsync(product, ct);
await _uow.SaveChangesAsync(ct);
// product.CreatedAt = DateTime.UtcNow  ← 自动
// product.CreatedBy = "admin"         ← 自动（来自 ICurrentUserService）
```

### 权限鉴权 / Permission Authorization

```csharp
// ✅ 使用 [Permission("code")] 属性
[Permission("products.delete")]
public async Task<IActionResult> Delete(Guid id) { ... }

// ✅ 不需要手动写角色检查代码
// ❌ 不要写：if (!User.IsInRole("Admin")) return Forbid();
```

### 异常处理 / Exception Handling

```csharp
// ✅ 抛出 BusinessException（由 GlobalExceptionMiddleware 捕获）
throw new BusinessException("库存不足", ErrorCode.InvalidOperation);

// ✅ 返回 ApiResult.Fail
return ApiResult<ProductDto>.Fail(ErrorCode.DataNotFound, "产品不存在");

// ❌ 不要直接返回 500 或 BadRequest
```

### 软删除 / Soft Delete

```csharp
// ✅ 推荐：软删除（保留数据，全局过滤器自动排除）
await _uow.Repository<Product>().SoftDeleteAsync(product, ct);
await _uow.SaveChangesAsync(ct);

// ✅ 也可以：物理删除（需要手动显式操作）
_uow.Repository<Product>().Delete(product);
await _uow.SaveChangesAsync(ct);
```

## 用户上下文注入 / ICurrentUserService

```csharp
// 在任意 Service / Controller 中注入
public class NotificationService
{
    private readonly ICurrentUserService _user;

    public async Task SendAsync(string message)
    {
        var userId = _user.UserId;          // Guid? 当前用户 ID
        var username = _user.UserName;      // string? 当前用户名
        var roles = _user.Roles;              // IReadOnlyList<string> 角色
        var isAuthenticated = _user.IsAuthenticated;  // bool
        // ...
    }
}
```

## Swagger 调试 / Swagger Debugging

```
① 在 Swagger 中找到 POST /api/auth/login
② 输入 {"username":"admin","password":"Admin@123"} → Execute
③ 复制响应中的 accessToken
④ 点击页面右上角 Authorize 🔒 按钮
⑤ 粘贴 accessToken → Authorize → Close
⑥ 所有 API 端点现在自动携带 Authorization: Bearer {token}
```

## 操作日志 / Operation Logging

### 添加操作日志

在需要记录操作日志的 Controller Action 上添加 `[OperationLog]` 特性即可：

```csharp
[HttpPost]
[Permission("users.create")]
[OperationLog("create", Resource = "User")]
public async Task<ApiResult<UserDto>> Create(CreateUserDto dto, CancellationToken ct)
{
    // 操作日志自动记录：操作人、操作类型、请求参数、IP、结果
}
```

### 特性参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `Action` | 操作类型（login / create / update / delete / export） | 必填 |
| `Resource` | 资源描述，为空时自动取 `controller.action` | null |
| `CaptureArgs` | 是否捕获请求参数快照 | true |

### 查询操作日志

```
GET /api/operation-logs?userId={guid}&action=create&username=admin&startTime=2026-01-01&endTime=2026-12-31&pageIndex=1&pageSize=20
```

> 日志通过 Hangfire 异步写入，不阻塞 HTTP 请求。写入失败不影响业务操作。

---

## 常见问题 / FAQ

### Q: 新增实体后需要手动迁移数据库吗？

不需要。开发环境 `DataSeeder` 在启动时自动 `EnsureCreated`，新增的实体只要在 `AppDbContext.OnModelCreating` 中配置即可自动建表。

### Q: Redis 必须安装吗？

不需要。`IPermissionService` 的 Redis 操作全部包裹 `try/catch(RedisConnectionException)`，自动降级到数据库查询。登录频控也仅在 Redis 可用时生效。

### Q: 如何让业务实体也自动审计？

继承 `AuditableEntity` 或 `SoftDeleteEntity` 即可，不需要额外配置。

## 密码校验 / Password Validation (v1.6)

使用 FluentValidation 自动校验，在 `Host/Validators/` 下添加校验器即可：

```csharp
public class CreateUserValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Password)
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("必须包含大写字母")
            .Matches("[a-z]").WithMessage("必须包含小写字母")
            .Matches("[0-9]").WithMessage("必须包含数字")
            .Matches("[^a-zA-Z0-9]").WithMessage("必须包含特殊字符");
    }
}
```

校验失败时自动返回 HTTP 400 + 中文错误消息，无需在 Controller 中手动处理。

## SMTP 邮件配置 / SMTP Configuration

SMTP 配置存储在 SystemParam 中（`smtp:default`），运行时修改无需重启：

```csharp
// Value 格式：JSON 字符串
{
  "Host": "smtp.qq.com",
  "Port": 587,
  "User": "admin@qq.com",
  "Password": "授权码",
  "From": "noreply@qq.com"
}
```

支持多配置：`smtp:alert`（告警专用）、`smtp:marketing`（营销专用）。为空则跳过发送。

## 审计日志快照 / Audit Snapshot

`AppDbContext.CaptureChangeSnapshot()` 自动捕获所有数据修改的 Before/After 值，OperationLogFilter 自动合并到日志 Detail 字段。无需手动干预。

## 健康检查扩展 / Health Check Extensions

```csharp
// 生产环境增强健康检查（需安装 NuGet 包）：
// AspNetCore.HealthChecks.Redis
// AspNetCore.HealthChecks.EntityFrameworkCore
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("DB")
    .AddRedis(connectionString, "Redis");
```
