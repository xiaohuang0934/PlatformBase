# PlatformBase API 版本管理 — 手把手教学笔记

---

## 一、为什么需要 API 版本管理？

| 场景 | 不版本化的后果 |
|------|---------------|
| 接口返回字段变更 | 旧版 App 崩溃（新增必填字段） |
| 接口路径重构 | 旧版 App 404 |
| 多版本并存 | 无法同时服务 v1 安卓版和 v2 iOS 版 |
| 灰度发布 | 无法逐步切流量到新版接口 |

**解决方案：** 同一接口支持多版本并存，客户端通过 URL 或 Header 指定版本。

---

## 二、本项目的版本策略

### 两种版本读取方式

```
方式 1: URL 路径段
  GET /api/v1/users    → ApiVersion = 1.0
  GET /api/v2/users    → ApiVersion = 2.0

方式 2: 查询字符串
  GET /api/users?api-version=1.0   → ApiVersion = 1.0
  GET /api/users?api-version=2.0   → ApiVersion = 2.0
```

### 配置代码

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);         // 默认 v1.0
    options.AssumeDefaultVersionWhenUnspecified = true;       // 不指定版本时用默认

    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),                     // 路径段读取
        new QueryStringApiVersionReader("api-version"));      // 查询字符串读取
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";       // Swagger 分组名: v1, v2
    options.SubstituteApiVersionInUrl = true; // 替换 URL 中的 {version} 占位符
});
```

---

## 三、使用示例

### 定义版本化控制器

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]  // {version:apiVersion} 占位符
[ApiVersion("1.0")]                                 // 标记此 Controller 属于 v1
public class UsersV1Controller : ControllerBase
{
    [HttpGet]
    public IActionResult GetUsers()
    {
        return Ok(new { version = "v1", users = new[] { "张三", "李四" } });
    }
}

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]                                 // v2 版本
public class UsersV2Controller : ControllerBase
{
    [HttpGet]
    public IActionResult GetUsers()
    {
        // v2 增加了分页和头像
        return Ok(new
        {
            version = "v2",
            total = 100,
            page = 1,
            users = new[] {
                new { name = "张三", avatar = "/avatars/1.png" },
                new { name = "李四", avatar = "/avatars/2.png" }
            }
        });
    }
}
```

### 请求示例

```bash
# v1 版本（URL 路径）
curl http://localhost:5269/api/v1/users
# {"version":"v1","users":["张三","李四"]}

# v2 版本（URL 路径）
curl http://localhost:5269/api/v2/users
# {"version":"v2","total":100,"page":1,"users":[...]}

# v2 版本（查询字符串）
curl http://localhost:5269/api/users?api-version=2.0
# {"version":"v2",...}

# 不指定版本 → 默认 v1
curl http://localhost:5269/api/users
# {"version":"v1",...}
```

### 单个 Controller 多版本映射

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/products")]
[ApiVersion("1.0", Deprecated = true)]  // v1 标记为已弃用
[ApiVersion("2.0")]                     // v2 为当前版本
public class ProductsController : ControllerBase
{
    [HttpGet]
    [MapToApiVersion("1.0")]
    public IActionResult GetProductsV1() => Ok(new { version = "v1" });

    [HttpGet]
    [MapToApiVersion("2.0")]
    public IActionResult GetProductsV2() => Ok(new { version = "v2" });
}
```

---

## 四、Swagger 集成

```csharp
builder.Services.AddSwaggerGen(options =>
{
    // 每个版本一个 Swagger 分组
    options.SwaggerDoc("v1", new()
    {
        Title = "PlatformBase API v1",
        Version = "v1",
        Description = "企业级 .NET 8 WebAPI 通用开发底座"
    });

    options.SwaggerDoc("v2", new()
    {
        Title = "PlatformBase API v2",
        Version = "v2",
        Description = "企业级 .NET 8 WebAPI — v2 新增分页功能"
    });
});
```

Swagger UI 中会出现版本下拉框，可切换查看不同版本的 API。

---

## 五、架构全景图

```
┌─────────────────────────────────────────────────────────────────┐
│                     API 版本管理架构                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  请求:                                                          │
│    GET /api/v2/users                                            │
│    GET /api/users?api-version=2.0                               │
│        │                                                        │
│        ▼                                                        │
│  ┌──────────────────┐                                           │
│  │ ApiVersionReader │  ← 解析版本号                              │
│  │ Combine:         │                                           │
│  │  UrlSegment      │  → {version:apiVersion} = "2"             │
│  │  QueryString     │  → ?api-version=2.0                       │
│  └────────┬─────────┘                                           │
│           │ version = 2.0                                       │
│           ▼                                                     │
│  ┌──────────────────┐                                           │
│  │ 路由匹配          │                                           │
│  │ UsersV2Controller │  ← [ApiVersion("2.0")]                   │
│  │ [Route("api/v{version}/users")]                              │
│  └──────────────────┘                                           │
│                                                                 │
│  配置:                                                          │
│    DefaultApiVersion = 1.0                                      │
│    AssumeDefaultVersionWhenUnspecified = true                    │
│    → 不指定版本时自动路由到 v1                                   │
│                                                                 │
│  Swagger:                                                       │
│    GroupNameFormat = "'v'VVV"                                    │
│    → 按版本分组文档 (v1, v2, v3...)                              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 六、最佳实践速查卡

```
┌─────────────────────────────────────────────────────────────────┐
│            API 版本管理 黄金法则                                  │
├─────────────────────────────────────────────────────────────────┤
│  1. URL 路径中加版本号：/api/v1/users（RESTful 最佳实践）        │
│  2. 同时支持查询字符串：?api-version=1.0（调试友好）             │
│  3. AssumeDefaultVersionWhenUnspecified = true（向后兼容）        │
│  4. Deprecated 标记旧版本（Swagger 中显式警告）                  │
│  5. 单 Controller 多版本用 [MapToApiVersion] 区分方法            │
│  6. Swagger 按版本分组文档                                      │
│  7. 做大变更时新建 Controller，小变更新增 Action                 │
│  8. 版本号用整数（v1, v2），不用语义版本（v1.2.3）               │
│  9. NuGet: Asp.Versioning（社区维护，比微软官方更灵活）            │
│ 10. 版本信息不仅限于 URL，也可用 Header: api-version: 2.0        │
└─────────────────────────────────────────────────────────────────┘
```
