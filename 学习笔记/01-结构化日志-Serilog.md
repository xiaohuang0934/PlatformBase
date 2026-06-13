# PlatformBase 结构化日志 (Serilog) — 手把手教学笔记

---

## 一、为什么用结构化日志？

### 1.1 传统日志的痛点

```csharp
// ❌ 传统写法 — 纯字符串
_logger.LogInformation("用户 " + username + " 在 " + DateTime.Now + " 登录成功，IP=" + ip);

// 输出：
// 用户 admin 在 2025/6/13 10:30:00 登录成功，IP=192.168.1.1

// 问题：
// 1. 无法按 username 过滤（只能 like '%admin%'）
// 2. 无法统计"admin 今天登录了几次"
// 3. 字符串拼接先执行再判断级别，Debug 级别也消耗 CPU
// 4. 更换输出格式需要改代码
```

### 1.2 结构化日志的优势

```csharp
// ✅ 结构化写法 — 属性模板
_logger.LogInformation("登录成功：用户 {Username}，IP={ClientIP}", username, ip);

// 输出：
// 登录成功：用户 admin，IP=192.168.1.1
//
// 底层数据结构（JSON）：
// {
//   "@t": "2025-06-13T10:30:00",
//   "@l": "Information",
//   "Username": "admin",
//   "ClientIP": "192.168.1.1",
//   "SourceContext": "PlatformBase.Host.IdentityServer.ResourceOwnerPasswordValidator"
// }

// 优势：
// 1. 可按 Username = "admin" 精确查询
// 2. 可聚合统计：SELECT Username, COUNT(*) GROUP BY Username
// 3. 模板渲染延迟到确认输出时，Debug 级别零消耗
// 4. 输出格式由 Sink 统一控制，代码无关
```

---

## 二、环境搭建 — 从零引入 Serilog

### 2.1 安装 NuGet 包

*`PlatformBase.Host.csproj:25`*

```xml
<!-- Serilog.AspNetCore 元包，一次性包含：
     Serilog 核心 + Console Sink + File Sink
     + Environment Enricher + Hosting 集成 -->
<PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
```

### 2.2 JSON 配置详解

*`appsettings.json:58-87`*

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

### 2.3 在 Program.cs 中注册

```csharp
var builder = WebApplication.CreateBuilder(args);

// 步骤 1：替换默认日志提供者（在 builder.Build() 之前）
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);  // 自动绑定 JSON
});

var app = builder.Build();

// 步骤 2：启用请求日志中间件
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress);
    };
});
```

---

## 三、日志注入 — 3 种方式

### 3.1 方式 1：构造函数注入 `ILogger<T>`（推荐）

```csharp
using Microsoft.Extensions.Logging;  // ← 注意：不是 Serilog

public class ResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
{
    private readonly ILogger<ResourceOwnerPasswordValidator> _logger;

    public ResourceOwnerPasswordValidator(ILogger<ResourceOwnerPasswordValidator> logger)
    {
        _logger = logger;
    }

    public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        var user = await FindUser(context.UserName);
        if (user == null)
        {
            _logger.LogWarning("登录失败：用户 {Username} 不存在", context.UserName);
            return;
        }
        _logger.LogInformation("登录成功：用户 {Username} ({UserId})", user.Username, user.Id);
    }
}
// 输出示例：
// [2025-06-13 10:30:00 WRN] PlatformBase.Host.IdentityServer.ResourceOwnerPasswordValidator: 登录失败：用户 zhangsan 不存在
```

### 3.2 方式 2：`ILoggerFactory` 手动创建

```csharp
// 适用于静态方法 / 启动阶段 / 非 DI 管理的场景
public static class DataSeeder
{
    public static async Task SeedAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DataSeeder");  // 自定义 category 名

        logger.LogInformation("种子数据初始化完成");
    }
}
```

### 3.3 方式 3：工厂方法中延迟解析

```csharp
public static IServiceCollection AddEventBus(this IServiceCollection services, Assembly assembly)
{
    services.AddSingleton<IEventPublisher, ChannelEventBus>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<ChannelEventBus>>();
        return new ChannelEventBus(sp, logger);
    });
    return services;
}
```

---

## 四、日志级别实战

```
Verbose → Debug → Information → Warning → Error → Critical
  0         1          2           3         4         5
```

### 4.1 Debug — 调试信息（生产不输出）

```csharp
// 高频操作只记 Debug，避免日志爆炸
_logger.LogDebug("权限鉴权通过：用户={User} 权限={Permission}", userName, code);
_logger.LogDebug("事件 {EventType} 无注册的处理器，跳过", typeof(TEvent).Name);
```

### 4.2 Information — 业务里程碑

```csharp
_logger.LogInformation("登录成功：用户 {Username} ({UserId})", user.Username, user.Id);
logger.LogInformation("种子数据初始化完成");
```

### 4.3 Warning — 预期内的业务异常

```csharp
// 密码错误 → 用户输入错误，不是系统问题
_logger.LogWarning("登录失败：用户 {Username} 密码错误（失败次数：{Count}）", user.Username, count);
// 账号锁定 → 安全机制触发
_logger.LogWarning("登录失败：用户 {Username} 已被锁定，剩余 {Minutes} 分钟", user.Username, min);
// 并发冲突 → EF Core 乐观锁的正常保护
_logger.LogWarning(ex, "并发冲突：数据已被他人修改");
// 权限拒绝 → 可能越权攻击，需要记录
_logger.LogWarning("权限鉴权拒绝：用户={User} 权限={Permission}", userName, code);
```

### 4.4 Error — 系统异常（需人工介入）

```csharp
// 未预期的 Exception → 这是 Bug
_logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
// 事件处理器崩溃
_logger.LogError(ex, "事件处理失败: {EventType} → {HandlerType}", eventType, handlerType);
// 鉴权服务异常
_logger.LogError(ex, "权限鉴权异常：用户={User} 权限={Permission}", userName, code);
```

### 4.5 异常类型 → 日志级别映射

```
catch (DbUpdateConcurrencyException ex)  → LogWarning  (并发是正常保护)
catch (BusinessException ex)             → LogWarning  (业务规则，预期内)
catch (Exception ex)                     → LogError    (Bug，需要修复)
```

---

## 五、结构化属性命名规范

### 5.1 核心规则

```csharp
// ✅ 正确：模板语法（保留结构）
_logger.LogInformation("用户 {Username} 登录成功", username);

// ❌ 错误 1：字符串插值（丢失结构）
_logger.LogInformation($"用户 {username} 登录成功");

// ❌ 错误 2：异常没放第一个参数（堆栈丢失）
_logger.LogError("出错：{Detail}", detail, ex);

// ✅ 正确 2：异常必须放第一个参数
_logger.LogError(ex, "出错：{Detail}", detail);
```

### 5.2 本项目的命名约定

| 属性名 | 出现次数 | 含义 |
|--------|----------|------|
| `{Username}` | 6 | 用户名 |
| `{Permission}` | 4 | 权限编码 |
| `{User}` / `{UserId}` | 3 | 用户对象 / ID |
| `{EventType}` / `{HandlerType}` | 2 | 事件和处理器类型 |
| `{Message}` | 2 | 异常消息 |
| `{Recipient}` | 1 | 邮件接收者 |
| `{TraceId}` / `{ClientIP}` | 各 1 | 由中间件添加 |

---

## 六、输出模板精讲

```
[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}
```

| 占位符 | 含义 | 示例 |
|--------|------|------|
| `{Timestamp}` | 时间戳（格式化） | `2025-06-13 10:30:00` |
| `{Level:u3}` | 级别 3 字符大写 | `INF` / `WRN` / `ERR` |
| `{SourceContext}` | 日志来源类全名 | `PlatformBase.Host.Middleware.GlobalExceptionMiddleware` |
| `{Message:lj}` | 消息（l=去引号，j=字面量） | `登录成功：用户 admin` |
| `{NewLine}` | 换行 | `\n` |
| `{Exception}` | 异常堆栈 | `System.NullReferenceException...` |

---

## 七、项目日志全景图

```
                        ┌─────────────────────┐
                        │   appsettings.json   │
                        │   Serilog 节点       │
                        └─────────┬───────────┘
                                  │ ReadFrom.Configuration
                                  ▼
┌─────────────┐     ┌────────────────────────┐     ┌──────────────────┐
│ Program.cs  │────▶│ builder.Host            │     │ 请求日志中间件    │
│  启动注册    │     │   .UseSerilog(...)      │     │ UseSerilog-      │
│             │     │  替换默认 ILogger        │     │ RequestLogging   │
└─────────────┘     └───────────┬────────────┘     │ + TraceId        │
                                │                   │ + ClientIP       │
                                ▼                   └────────┬─────────┘
              ┌─────────────────────────────────┐            │
              │        ILogger<T> 注入           │◀───────────┘
              │  GlobalExceptionMiddleware       │
              │  PermissionAuthorizationHandler  │         ┌──────────┐
              │  ResourceOwnerPasswordValidator  │     ───▶│ Console  │
              │  ProfileService                  │     │   └──────────┘
              │  ChannelEventBus                 │     │
              │  SmtpChannelProvider             │     │   ┌──────────────┐
              └─────────────────────────────────┘     ───▶│ File Sink    │
                                                         │ logs/log-.txt│
                                                         │ 保留30天      │
                                                         └──────────────┘
```

---

## 八、最佳实践速查卡

```
┌─────────────────────────────────────────────────────────────────┐
│                    Serilog 黄金法则                              │
├─────────────────────────────────────────────────────────────────┤
│  1. 永远用 {Property} 模板，不用 $"{var}" 插值                   │
│  2. Exception 对象永远放第一个参数                                │
│  3. 业务异常 = Warning，系统异常 = Error                         │
│  4. 高频操作用 Debug，生产不刷屏                                 │
│  5. 框架日志压到 Warning                                         │
│  6. 配置与代码分离：ReadFrom.Configuration()                     │
│  7. 文件日志按天滚动 + 保留上限 30 天                             │
│  8. 构造函数注入 ILogger<T>，导入 Microsoft.Extensions.Logging   │
│  9. 非 DI 场景用 ILoggerFactory.CreateLogger("名称")            │
│ 10. Debug 模板不消耗性能：确定要输出才渲染                        │
└─────────────────────────────────────────────────────────────────┘
```
