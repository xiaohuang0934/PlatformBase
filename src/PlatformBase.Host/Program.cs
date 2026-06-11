using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using Asp.Versioning;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Tokens;
using PlatformBase.Application.Services;
using PlatformBase.Core;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Services;
using PlatformBase.Host.Authorization;
using PlatformBase.Host.Extensions;
using PlatformBase.Host.Filters;
using PlatformBase.Host.IdentityServer;
using PlatformBase.Host.Middleware;
using PlatformBase.Host.Services;
using PlatformBase.Host.NotificationProviders;
using PlatformBase.Host.StorageProviders;
using PlatformBase.Infrastructure.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════ 结构化日志 (Serilog) ═══════════════════
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

// ═══════════════════ 健康检查 ═══════════════════
builder.Services.AddHealthChecks();

// ═══════════════════ 用户会话上下文（AppDbContext 依赖它，必须在 AddDatabase 之前注册） ═══════════════════
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ═══════════════════ 数据库配置 ═══════════════════
var dbProvider = Enum.Parse<DatabaseProvider>(builder.Configuration["Database:Provider"] ?? "Sqlite");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["Database:ConnectionString"] ?? "Data Source=app.db";
var enableSensitiveLogging = builder.Configuration.GetValue<bool>("Database:EnableSensitiveDataLogging");
builder.Services.AddDatabase(dbProvider, connectionString, enableSensitiveLogging);

// ═══════════════════ Controllers + 全局过滤器 ═══════════════════
builder.Services.AddControllers(options =>
{
    options.Filters.Add<PlatformBase.Host.Filters.OperationLogFilter>();
    options.Filters.Add<PlatformBase.Host.Filters.RateLimitFilter>();
    options.Filters.Add<PlatformBase.Host.Filters.DataScopeFilter>();
});
builder.Services.AddEndpointsApiExplorer();

// ═══════════════════ 业务服务注册 ═══════════════════
// 根据命名约定自动扫描：Application 层 I*Service → Host 层 *Service，统一注册为 Scoped
builder.Services.AddApplicationServices();
builder.Services.AddScoped<PersistedGrantStore>(); // IdentityServer4，无接口，特殊注册

// ═══════════════════ 事件总线（Channel 实现，预留 RabbitMQ 切换）══════════════════
builder.Services.AddEventBus(typeof(ChannelEventBus).Assembly);

// ═══════════════════ 手动注册（非 I*Service 约定，接口在 Host/Core 层）══════════════════
builder.Services.AddSingleton<IFileStorageProvider, LocalFileStorageProvider>();
builder.Services.AddScoped<IExportService, ImportExportService>();
builder.Services.AddScoped<IImportService, ImportExportService>();
builder.Services.AddSingleton<ILockService, RedisLockService>();
builder.Services.AddSingleton<IIdGenerator, GuidIdGenerator>();

// ═══════════════════ 后台任务调度 (Hangfire) ═══════════════════
builder.Services.AddHangfireInfrastructure(dbProvider, connectionString);

// ═══════════════════ 签名密钥（IdentityServer4 签发 + JwtBearer 验签共用同一密钥） ═══════════════════
// 使用 X509 自签名证书（纯托管代码，跨平台兼容，无 Apple RSA 实现兼容性问题）
using var rsa = RSA.Create(2048);
var certRequest = new CertificateRequest(
    "CN=PlatformBase", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var cert = certRequest.CreateSelfSigned(
    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
var signingKey = new X509SecurityKey(cert) { KeyId = Guid.NewGuid().ToString() };

// ═══════════════════ IdentityServer4 (OAuth2/OIDC Token 签发服务) ═══════════════════
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;
})
.AddResourceOwnerValidator<ResourceOwnerPasswordValidator>()
.AddProfileService<ProfileService>()
.AddSigningCredential(new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256))
.AddPersistedGrantStore<PersistedGrantStore>()   // 方案 B：refresh_token 持久化到数据库
.AddInMemoryApiResources(Config.ApiResources)
.AddInMemoryApiScopes(Config.ApiScopes)
.AddInMemoryIdentityResources(Config.IdentityResources)
.AddInMemoryClients(Config.Clients);

// ═══════════════════ JWT Bearer 认证（与 IdentityServer 共享签名密钥，无 HTTP 发现） ═══════════════════
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,         // 直接使用内存中的同一密钥
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["IdentityServer:Authority"] ?? "http://localhost:5269",
        ValidateAudience = true,
        ValidAudience = "api1",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };

    // 统一认证失败的响应格式为 ApiResult
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            var result = ApiResult.Fail(ErrorCode.Unauthorized, "认证失败，请重新登录");
            await context.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(result,
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        },
        OnForbidden = async context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            var result = ApiResult.Fail(ErrorCode.Unauthorized, "认证失败，请重新登录");
            await context.Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(result,
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        }
    };
});

// ═══════════════════ 权限鉴权体系 ═══════════════════
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ═══════════════════ Redis 缓存 ═══════════════════
builder.Services.AddRedis(builder.Configuration);

// ═══════════════════ API 版本管理 ═══════════════════
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new QueryStringApiVersionReader("api-version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ═══════════════════ 国际化（多语言）══��═══════════════
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("zh-CN");
    options.SupportedCultures = [new CultureInfo("zh-CN"), new CultureInfo("en")];
    options.SupportedUICultures = [new CultureInfo("zh-CN"), new CultureInfo("en")];
    options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
});

// ═══════════════════ Swagger / OpenAPI 文档 + Bearer Token 安全 + API 版本分组 ═══════════════════
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "PlatformBase API v1",
        Version = "v1",
        Description = "企业级 .NET 8 WebAPI 通用开发底座 — 认证授权 & 权限管理"
    });

    options.AddJwtSecurity();
    options.OperationFilter<SwaggerDefaultValues>();
});

// ═══════════════════ 跨域配置 (CORS) ═══════════════════
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["*"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ═══════════════════ 中间件管道（顺序敏感，不可随意调换） ═══════════════════

// ① 全局异常处理 — 管道最前端，捕获所有后续中间件的异常
app.UseMiddleware<GlobalExceptionMiddleware>();

// ①½ 国际化请求本地化 — 解析 Accept-Language 头，设置 CultureInfo
app.UseRequestLocalization();

// ② 路由匹配 — 必须在 UseIdentityServer 之前
app.UseRouting();

// ③ 跨域 — 必须在路由之后
app.UseCors();

// ④ IdentityServer4 — 拦截 /connect/token 和 /.well-known/* 端点
app.UseIdentityServer();

// ⑤ JWT Bearer 认证 — 验证请求中的 Authorization 头
app.UseAuthentication();

// ⑤½ 安全戳验证 — 密码变更后旧 token 即时失效（方案 A：Redis + DB 双校验）
app.UseMiddleware<StampValidationMiddleware>();

// ⑥ 权限鉴权 — 根据 [Permission("code")] 属性验证用户权限
app.UseAuthorization();

// ⑥½ Hangfire Dashboard — 任务调度监控面板（仅 Admin 角色）
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthFilter()],
    DashboardTitle = "PlatformBase 任务调度"
});

// ⑦ Swagger UI — 仅开发环境
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PlatformBase API v1");
    });
}

// ⑧ API 控制器端点
app.MapControllers();

// ═══════════════════ 种子数据初始化（幂等） ═══════════════════
await app.SeedAsync();

// ═══════════════════ 后台任务同步（从 JobSchedules 表读取配置，注册到 Hangfire） ═══════════════════
await app.UseHangfireSyncAsync();

app.Run();
