using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Tokens;
using PlatformBase.Core;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Services;
using PlatformBase.Host.Authorization;
using PlatformBase.Host.Extensions;
using PlatformBase.Host.Filters;
using PlatformBase.Host.IdentityServer;
using PlatformBase.Host.Middleware;
using PlatformBase.Host.NotificationProviders;
using PlatformBase.Host.StorageProviders;
using PlatformBase.Host.Validators;
using PlatformBase.Infrastructure.Extensions;
using Serilog;

// ============================================================================
// 实现逻辑总览：
//   1. 构建 WebApplication 并注册所有依赖服务（DI 容器）
//   2. 配置 Serilog 结构化日志 → 健康检查 → 优雅关闭 → 用户上下文 → 数据库
//   3. 配置 Controllers + 全局过滤器（操作日志/限流/数据权限）
//   4. 配置 FluentValidation 自动校验
//   5. 注册业务服务（按命名约定自动扫描 / 手动注册特殊接口）
//   6. 配置事件总线（Channel 内存实现，预留 RabbitMQ 切换）
//   7. 配置 Hangfire 后台任务调度
//   8. 使用 X509 自签名证书生成签名密钥（IdentityServer + JWT 共用）
//   9. 配置 IdentityServer4（OAuth2/OIDC Token 签发服务）
//  10. 配置 JWT Bearer 认证（与 IdentityServer 共享密钥，无需 HTTP 发现）
//  11. 配置权限鉴权体系（Permission 策略提供者 + 处理程序）
//  12. 配置 Redis 缓存 → API 版本管理 → 国际化 → Swagger → CORS
//  13. 构建中间件管道（顺序敏感：异常→日志→本地化→路由→CORS→IS4→认证→安全戳→鉴权→Hangfire→Swagger→Controllers）
//  14. 种子数据初始化 + Hangfire 任务同步
// ============================================================================

var builder = WebApplication.CreateBuilder(args); // 创建 Web 应用构建器，读取 appsettings.json + 环境变量 + 命令行参数

// ═══════════════════ 结构化日志 (Serilog) ═══════════════════
builder.Host.UseSerilog((context, config) => // 用 Serilog 替换默认的 Microsoft.Extensions.Logging
{
    config.ReadFrom.Configuration(context.Configuration); // 从 appsettings.json 的 Serilog 节点读取日志配置（输出目标/级别）
});

// ═══════════════════ 健康检查（DB 连接） ═══════════════════
builder.Services.AddHealthChecks(); // 注册健康检查服务，K8s / 负载均衡器可通过 /health 端点探活

// ═══════════════════ 优雅关闭（Host 层级，不阻塞线程） ═══════════════════
builder.Services.Configure<HostOptions>(options => // 配置 Host 级别的选项
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(15); // 收到 SIGTERM 后最多等待 15 秒完成正在处理的请求再退出
});

// ═══════════════════ 用户会话上下文（AppDbContext 依赖它，必须在 AddDatabase 之前注册） ═══════════════════
builder.Services.AddHttpContextAccessor(); // 注册 IHttpContextAccessor，使服务能访问当前 HTTP 请求上下文
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>(); // 每次请求创建一个 CurrentUserContext 实例

// ═══════════════════ 数据库配置 ═══════════════════
var dbProvider = Enum.Parse<DatabaseProvider>(builder.Configuration["Database:Provider"] ?? "Sqlite"); // 从配置读取数据库类型
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") // 优先从 ConnectionStrings 节点读取
    ?? builder.Configuration["Database:ConnectionString"] ?? "Data Source=app.db"; // 回退到 Database:ConnectionString，最后用默认 SQLite 路径
var enableSensitiveLogging = builder.Configuration.GetValue<bool>("Database:EnableSensitiveDataLogging"); // 是否在异常中暴露 SQL 参数值（仅调试）
builder.Services.AddDatabase(dbProvider, connectionString, enableSensitiveLogging); // 注册 AppDbContext 及对应数据库提供程序

// ═══════════════════ Controllers + 全局过滤器 ═══════════════════
builder.Services.AddControllers(options => // 注册 MVC 控制器服务
{
    options.Filters.Add<OperationLogFilter>(); // 全局操作日志过滤器（记录增删改查操作）
    options.Filters.Add<RateLimitFilter>();    // 全局限流过滤器（Redis 滑动窗口）
    options.Filters.Add<DataScopeFilter>();    // 全局数据权限过滤器（部门级数据隔离）
}).AddJsonOptions(opt => // 配置 JSON 序列化选项
{
    opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; // 属性命名策略：驼峰命名（符合前端习惯）
});
builder.Services.AddEndpointsApiExplorer(); // 注册 Endpoint 元数据发现（Swagger 生成所需）

// ═══════════════════ FluentValidation 自动校验 ═══════════════════
builder.Services.AddFluentValidationAutoValidation() // 启用自动模型校验（替代 [ApiController] 的默认校验）
    .AddValidatorsFromAssemblyContaining<CreateUserValidator>(); // 从 CreateUserValidator 所在程序集扫描所有 IValidator<T> 并注册

// ═══════════════════ 业务服务注册 ═══════════════════
// 根据命名约定自动扫描：Application 层 I*Service → Host 层 *Service，统一注册为 Scoped
builder.Services.AddApplicationServices(); // 扩展方法：遍历 Application 程序集的接口，找到 Host 层的实现，自动 DI 注册
builder.Services.AddScoped<PersistedGrantStore>(); // IdentityServer4 持久化授权存储，无接口，需手动注册

// ═══════════════════ 事件总线（Channel 实现，预留 RabbitMQ 切换）══════════════════
builder.Services.AddEventBus(typeof(ChannelEventBus).Assembly); // 扫描 ChannelEventBus 所在程序集中的 IEvent / IEventHandler，注册事件总线

// ═══════════════════ 手动注册（非 I*Service 约定，接口在 Host/Core 层）══════════════════
builder.Services.AddSingleton<IFileStorageProvider, LocalFileStorageProvider>(); // 文件存储：本地文件系统（后期可替换 OSS/S3）
builder.Services.AddScoped<IExportService, ImportExportService>();      // 数据导出服务（Excel/CSV）
builder.Services.AddScoped<IImportService, ImportExportService>();      // 数据导入服务（同一实现类，两个接口）
builder.Services.AddScoped<ImportExportService>();                      // 注册具体类，供内部使用（无需接口）
builder.Services.AddSingleton<ILockService, RedisLockService>();        // 分布式锁服务（基于 Redis SETNX）
builder.Services.AddSingleton<IIdGenerator, GuidIdGenerator>();         // 唯一 ID 生成器（Guid 实现，可替换雪花算法）
builder.Services.AddSingleton<IChannelProvider, InAppChannelProvider>(); // 站内信通知通道
builder.Services.AddSingleton<IChannelProvider, SmtpChannelProvider>(); // 邮件通知通道（SMTP）

// ═══════════════════ 后台任务调度 (Hangfire) ═══════════════════
builder.Services.AddHangfireInfrastructure(dbProvider, connectionString); // 根据数据库类型注册 Hangfire 存储（SQL Server / PostgreSQL / SQLite）

// ═══════════════════ 签名密钥（IdentityServer4 签发 + JwtBearer 验签共用同一密钥） ═══════════════════
// 生产环境从文件加载 .pfx 证书，开发环境自动生成内存证书
var certPath = builder.Configuration["IdentityServer:SigningCertPath"];
var certPassword = builder.Configuration["IdentityServer:SigningCertPassword"] ?? "";
var cert = !string.IsNullOrEmpty(certPath) && File.Exists(certPath)
    ? new X509Certificate2(certPath, certPassword)           // 生产：加载持久化证书（重启后旧 Token 仍有效）
    : CreateDevelopmentCertificate();                        // 开发：每次重启重新生成
var signingKey = new X509SecurityKey(cert) { KeyId = Guid.NewGuid().ToString() }; // 将证书包装为 JWT 签名/验签密钥，KeyId 用于 kid 头标识

static X509Certificate2 CreateDevelopmentCertificate()
{
    using var rsa = RSA.Create(2048); // 创建 2048 位 RSA 密钥对（using 确保及时释放非托管资源）
    var certRequest = new CertificateRequest( // 构造 X509 证书签名请求（CSR）
        "CN=PlatformBase", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); // 主题=PlatformBase，签名算法=SHA256+RSA-PKCS1
    return certRequest.CreateSelfSigned( // 自签名生成证书（无需 CA 根证书）
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1)); // 有效期：从当前 UTC 时间起 1 年
}

// ═══════════════════ IdentityServer4 (OAuth2/OIDC Token 签发服务) ═══════════════════
builder.Services.AddIdentityServer(options => // 注册 IdentityServer4 服务
{
    options.Events.RaiseErrorEvents = true;       // 启用错误事件（便于诊断）
    options.Events.RaiseInformationEvents = true; // 启用信息事件
    options.Events.RaiseFailureEvents = true;     // 启用失败事件
    options.Events.RaiseSuccessEvents = true;     // 启用成功事件
})
.AddResourceOwnerValidator<ResourceOwnerPasswordValidator>() // 资源所有者密码模式验证器（用户名+密码 → Token）
.AddProfileService<ProfileService>()                                   // 自定义 Profile 服务（决定 Token 中包含哪些 Claims）
.AddSigningCredential(new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)) // Token 签名凭证：RSA-SHA256
.AddPersistedGrantStore<PersistedGrantStore>()   // 方案 B：refresh_token 持久化到数据库（方案 A 是内存/Redis）
.AddInMemoryApiResources(Config.ApiResources)    // API 资源定义（内存模式，生产可换 DB）
.AddInMemoryApiScopes(Config.ApiScopes)          // API 作用域定义
.AddInMemoryIdentityResources(Config.IdentityResources) // 身份资源定义（openid / profile / email）
.AddInMemoryClients(Config.Clients);             // 客户端定义（client_id / secret / grant_type）

// ═══════════════════ JWT Bearer 认证（与 IdentityServer 共享签名密钥，无 HTTP 发现） ═══════════════════
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme) // 注册认证服务，默认方案=JWT Bearer
.AddJwtBearer(options => // 配置 JWT Bearer 认证参数
{
    options.TokenValidationParameters = new TokenValidationParameters // 令牌校验参数
    {
        ValidateIssuerSigningKey = true,  // 校验签名密钥
        IssuerSigningKey = signingKey,    // 直接使用内存中的同一密钥（与 IS4 共用，无需从 /.well-known 拉取）
        ValidateIssuer = true,            // 校验 Token 签发方
        ValidIssuer = builder.Configuration["IdentityServer:Authority"] ?? "http://localhost:5269", // 合法签发方地址
        ValidateAudience = true,          // 校验 Token 受众
        ValidAudience = "api1",           // 合法受众标识
        ValidateLifetime = true,          // 校验 Token 有效期
        ClockSkew = TimeSpan.FromMinutes(1), // 客户端与服务器时钟偏差容忍 1 分钟
        NameClaimType = ClaimTypes.Name,  // 将 OAuth "name" 映射到 .NET ClaimTypes.Name
        RoleClaimType = ClaimTypes.Role   // 将 OAuth "role" 映射到 .NET ClaimTypes.Role
    };

    // JWT 认证事件回调
    options.Events = new JwtBearerEvents
    {
        // ① Token 过期时在响应头加标记，前端可据此自动刷新（无感刷新）
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                context.Response.Headers.Append("Token-Expired", "true"); // 前端检测此 Header → 调 refresh_token 换新 Token
            return Task.CompletedTask;
        },

        // ② SignalR WebSocket 握手不支持自定义 Header，从 URL 查询参数提取 Token
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"]; // SignalR 客户端将 Token 放在 URL 参数中
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                context.Token = accessToken; // 赋值后由后续 JwtBearer 中间件统一验证
            return Task.CompletedTask;
        },

        // ③ 认证失败（未登录或 Token 无效）统一返回 ApiResult 格式
        OnChallenge = async context =>
        {
            context.HandleResponse(); // 阻止默认 401 响应
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200; // HTTP 200 + body 中 code 表示错误类型
            var result = ApiResult.Fail(ErrorCode.Unauthorized, "认证失败，请重新登录");
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(result,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        },

        // ④ 鉴权失败（有 Token 但无权限）统一返回 ApiResult 格式
        OnForbidden = async context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            var result = ApiResult.Fail(ErrorCode.Forbidden, "没有访问权限");
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(result,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    };
});

// ═══════════════════ 权限鉴权体系 ═══════════════════
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>(); // 自定义策略提供者：将 [Permission("code")] 转为动态策略
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();      // 权限处理程序：查询用户权限并决策通过/拒绝

// ═══════════════════ Redis 缓存 ═══════════════════
builder.Services.AddRedis(builder.Configuration); // 注册 IConnectionMultiplexer 和 IDatabase（读取 Redis 配置节点）

// ═══════════════════ API 版本管理 ═══════════════════
builder.Services.AddApiVersioning(options => // 注册 API 版本控制服务
{
    options.DefaultApiVersion = new ApiVersion(1, 0); // 默认 API 版本：v1.0
    options.AssumeDefaultVersionWhenUnspecified = true; // 未指定版本时使用默认版本
    options.ApiVersionReader = ApiVersionReader.Combine( // 支持多种方式读取版本号
        new UrlSegmentApiVersionReader(),           // 方式 1：URL 路径段（/api/v1/users）
        new QueryStringApiVersionReader("api-version")); // 方式 2：查询字符串（?api-version=1.0）
}).AddApiExplorer(options => // 配置 API Explorer（Swagger 版本分组）
{
    options.GroupNameFormat = "'v'VVV";       // 版本组名格式：v1、v2（VVV=主版本号）
    options.SubstituteApiVersionInUrl = true; // 在 Swagger URL 模板中替换 {version} 占位符
});

// ═══════════════════ 国际化（多语言） ═══════════════════
builder.Services.AddLocalization(); // 注册本地化服务（IStringLocalizer / IHtmlLocalizer）
builder.Services.Configure<RequestLocalizationOptions>(options => // 配置请求本地化选项
{
    options.DefaultRequestCulture = new RequestCulture("zh-CN"); // 默认文化：中文（简体）
    options.SupportedCultures = [new CultureInfo("zh-CN"), new CultureInfo("en")]; // 支持的文化列表：中文/英文
    options.SupportedUICultures = [new CultureInfo("zh-CN"), new CultureInfo("en")]; // 支持的 UI 文化列表
    options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()]; // 从 Accept-Language 请求头解析语言
});

// ═══════════════════ Swagger / OpenAPI 文档 + Bearer Token 安全 + API 版本分组 ═══════════════════
builder.Services.AddSwaggerGen(options => // 注册 Swagger 文档生成服务
{
    options.SwaggerDoc("v1", new() // 创建 v1 版本的 API 文档分组
    {
        Title = "PlatformBase API v1", // 文档标题
        Version = "v1",                // 文档版本标识
        Description = "企业级 .NET 8 WebAPI 通用开发底座 — 认证授权 & 权限管理" // 文档描述
    });

    options.AddJwtSecurity(); // 添加 JWT Bearer Token 安全定义（Authorize 按钮）
    options.OperationFilter<SwaggerDefaultValues>(); // 应用 Swagger 默认值过滤器（版本信息等元数据）
});

// ═══════════════════ 跨域配置 (CORS) ═══════════════════
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["*"]; // 从配置读取允许的来源域名
builder.Services.AddCors(options => // 注册 CORS 服务
{
    options.AddDefaultPolicy(policy => // 添加默认 CORS 策略
    {
        policy.WithOrigins(corsOrigins) // 允许的来源域名列表（生产环境应指定具体域名）
              .AllowAnyHeader()         // 允许任意请求头
              .AllowAnyMethod();        // 允许任意 HTTP 方法（GET/POST/PUT/DELETE）
    });
});

var app = builder.Build(); // 构建 WebApplication 实例，完成所有服务注册

// ═══════════════════ 中间件管道（顺序敏感，不可随意调换） ═══════════════════

// ① 全局异常处理 — 管道最前端，捕获所有后续中间件的异常
app.UseMiddleware<GlobalExceptionMiddleware>(); // 将未处理异常转为 ApiResult 统一格式，避免泄露堆栈信息

// ①¼ 请求日志 — 记录每个请求的路径/方法/耗时/状态码到 Serilog
app.UseSerilogRequestLogging(options => // 启用 Serilog 请求日志中间件
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) => // 为每个日志事件附加额外信息
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier); // 附加追踪 ID（用于链路追踪）
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress); // 附加客户端 IP
    };
});

// ①½ 国际化请求本地化 — 解析 Accept-Language 头，设置 CultureInfo
app.UseRequestLocalization(); // 根据请求头设置当前线程的 CultureInfo，影响 .resx 资源文件选择

// ② 路由匹配 — 必须在 UseIdentityServer 之前
app.UseRouting(); // 根据 URL 路径匹配对应的 Controller/Action，填充 RouteValues

// ③ 跨域 — 必须在路由之后
app.UseCors(); // 应用默认 CORS 策略，处理 OPTIONS 预检请求

// ④ IdentityServer4 — 拦截 /connect/token 和 /.well-known/* 端点
app.UseIdentityServer(); // 将 IdentityServer4 中间件插入管道，处理 OAuth2/OIDC 协议端点

// ⑤ JWT Bearer 认证 — 验证请求中的 Authorization 头
app.UseAuthentication(); // 解析 JWT Token，填充 HttpContext.User（ClaimsPrincipal）

// ⑤½ 安全戳验证 — 密码变更后旧 token 即时失效（方案 A：Redis + DB 双校验）
app.UseMiddleware<StampValidationMiddleware>(); // 检查 User 的 SecurityStamp 是否与数据库一致，不一致则返回 401

// ⑥ 权限鉴权 — 根据 [Permission("code")] 属性验证用户权限
app.UseAuthorization(); // 执行 [Authorize] / [Permission] 策略评估，决定是否允许访问

// ⑥½ Hangfire Dashboard — 任务调度监控面板（仅 Admin 角色）
app.UseHangfireDashboard("/hangfire", new DashboardOptions // 将 Hangfire 仪表盘挂载到 /hangfire 路径
{
    Authorization = [new HangfireAuthFilter()], // 自定义授权过滤器：仅允许管理员角色访问
    DashboardTitle = "PlatformBase 任务调度"    // 仪表盘页面标题
});

// ⑦ Swagger UI — 仅开发环境
if (app.Environment.IsDevelopment()) // 仅在开发环境中启用 Swagger UI（生产环境出于安全考虑应关闭）
{
    app.UseSwagger(); // 启用 Swagger JSON 文档生成端点（/swagger/v1/swagger.json）
    app.UseSwaggerUI(options => // 启用 Swagger UI 交互式文档页面（/swagger）
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PlatformBase API v1"); // 指定文档端点 URL 及显示名称
    });
}

// ⑧ API 控制器端点
app.MapControllers(); // 将 Controller 路由映射到管道终端，匹配到的方法即为请求处理程序

// ═══════════════════ 种子数据初始化（幂等） ═══════════════════
await app.SeedAsync(); // 执行数据库种子数据初始化（创建租户/管理员角色/默认权限等），重复执行安全

// ═══════════════════ 后台任务同步（从 JobSchedules 表读取配置，注册到 Hangfire） ═══════════════════
await app.UseHangfireSyncAsync(); // 从数据库 JobSchedules 表读取定时任务配置并注册为 Hangfire RecurringJob

app.Run(); // 启动 Kestrel 服务器，开始监听 HTTP 请求（阻塞当前线程直到进程终止）
