using PlatformBase.Core;
using PlatformBase.Host.Middleware;
using PlatformBase.Infrastructure.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ========== 结构化日志 (Serilog) ==========
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

// ========== 健康检查 ==========
builder.Services.AddHealthChecks();

// ========== 数据库配置 ==========
var dbProvider = Enum.Parse<DatabaseProvider>(builder.Configuration["Database:Provider"] ?? "Sqlite");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["Database:ConnectionString"] ?? "Data Source=app.db";
var enableSensitiveLogging = builder.Configuration.GetValue<bool>("Database:EnableSensitiveDataLogging");
builder.Services.AddDatabase(dbProvider, connectionString, enableSensitiveLogging);

// ========== Controllers + FluentValidation ==========
builder.Services.AddControllers();

// ========== Swagger / OpenAPI 文档 ==========
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "PlatformBase API",
        Version = "v1",
        Description = "企业级平台项目通用底座 API"
    });
});

// ========== 跨域配置 (CORS) ==========
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

// ========== 中间件管道 ==========

// 全局异常处理——管道最前端，确保所有异常都能被捕获
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger 仅开发环境启用
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS 必须在路由之前
app.UseCors();

app.MapControllers();

app.Run();
