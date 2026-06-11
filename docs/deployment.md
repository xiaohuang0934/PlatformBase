# 部署指南 / Deployment Guide

## ⚠️ 生产环境必须：EF Core Migration

**开发环境：** 项目使用 `DataSeeder.EnsureCreatedAsync()` 自动建表，适配快速迭代。

**生产环境：** **绝对禁止使用 `EnsureCreated`。** 原因：

```
v1.0: Users / Roles / Permissions 6 张表 → EnsureCreated ✅
v1.1: 新增 4 张表 → EnsureCreated ❌ 失败！
      表已存在，无法增量添加。只能删库重建 → 生产数据全部丢失。
```

**必须切换到 EF Core Migration：**

```bash
# 1. 安装工具
dotnet tool install --global dotnet-ef

# 2. 生成增量迁移脚本（每次加表/改表执行一次）
dotnet ef migrations add AddNewModule --project src/PlatformBase.Infrastructure

# 3. 执行迁移（部署时自动或手动执行）
dotnet ef database update --project src/PlatformBase.Infrastructure
```

> **数据种子：** Migration 场景下 `EnsureCreatedAsync` 不会执行。需在迁移脚本中调用 `DataSeeder.SeedAsync`，或在应用启动时检查是否需要种子。

---

## 生产环境准备 / Production Readiness Checklist

| # | 检查项 / Item | 开发环境 | 生产环境 |
|---|--------------|---------|---------|
| 1 | 签名证书 | X509 自签名（每次启动生成） | 真实 X509 证书（长期有效） |
| 2 | JWT Secret | appsettings.json 明文 | 环境变量 / Secret Manager |
| 3 | 数据库 | SQLite | SQL Server / MySQL |
| 4 | Redis | localhost:6379 | 独立 Redis 服务（集群） |
| 5 | Swagger | 开启 | 关闭 |
| 6 | CORS | `*` | 具体域名白名单 |
| 7 | HTTPS | 关闭 | 开启（反向代理或 Kestrel） |
| 8 | 日志 | Console + File | File + 集中式日志 |

## 证书配置 / Certificate Configuration

### 开发环境（当前）

应用启动时自动生成 X509 自签名证书：

```csharp
using var cert = new CertificateRequest(...).CreateSelfSigned(...);
var signingKey = new X509SecurityKey(cert);
```

### 生产环境 — 证书文件部署 / Certificate File

```bash
# 生成证书
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem \
  -days 1095 -nodes -subj "/CN=PlatformBase.IdentityServer"

openssl pkcs12 -export -out identityserver.pfx \
  -inkey key.pem -in cert.pem -passout pass:YourPassword
```

```csharp
// Program.cs 中替换自签名证书
var cert = new X509Certificate2("identityserver.pfx", "YourPassword");
var signingKey = new X509SecurityKey(cert);
```

### 生产环境 — Base64 方式（推荐 K8s / 云部署）

```jsonc
// appsettings.Production.json
"IdentityServer": {
  "SigningCertSource": "Base64",
  "SigningCertBase64": "MIIKEgIBAzCC...", // base64 cert
  "SigningCertPassword": "YourPassword"
}
```

```csharp
var certBytes = Convert.FromBase64String(config["SigningCertBase64"]);
var cert = new X509Certificate2(certBytes, config["SigningCertPassword"]);
```

## 环境变量配置 / Environment Variables

生产环境敏感信息应从环境变量注入：

| 配置 | 环境变量 | 说明 |
|------|---------|------|
| ConnectionString | `ConnectionStrings__DefaultConnection` | 数据库连接串 |
| JWT Secret | `Jwt__Secret` | JWT 签名密钥 |
| Redis | `Redis__ConnectionString` | Redis 连接串 |

```bash
export ConnectionStrings__DefaultConnection="Server=prod-db;Database=PlatformBase;..."
export Jwt__Secret="prod-super-secret-key-min-32-chars-long!!"
export Redis__ConnectionString="redis-cluster:6379,password=xxx"
```

## HTTPS / TLS

### 反向代理方案（推荐）

```
nginx / Azure Front Door / Cloudflare
  └── TLS termination
      └── http://localhost:5269 (Kestrel)
```

nginx 示例：

```nginx
server {
    listen 443 ssl;
    server_name api.platformbase.com;
    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location / {
        proxy_pass http://localhost:5269;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### Kestrel 直接 HTTPS

```csharp
// Program.cs
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 443, listenOptions =>
    {
        listenOptions.UseHttps("certificate.pfx", "password");
    });
});
```

## 数据库迁移 / Database Migration

### 开发环境（当前）

```csharp
// DataSeeder 自动 EnsureCreated
await context.Database.EnsureCreatedAsync();
```

### 生产环境

使用 EF Core Migration：

```bash
# 安装 EF Core CLI
dotnet tool install --global dotnet-ef

# 生成迁移脚本
dotnet ef migrations add InitialCreate \
  --project src/PlatformBase.Infrastructure \
  --startup-project src/PlatformBase.Host

# 生成 SQL 脚本（审核后执行）
dotnet ef migrations script \
  --project src/PlatformBase.Infrastructure \
  --startup-project src/PlatformBase.Host \
  -o migration.sql

# 执行迁移
dotnet ef database update \
  --project src/PlatformBase.Infrastructure \
  --startup-project src/PlatformBase.Host
```

> **注意**：生产环境不应使用 `EnsureCreated`（不会追踪迁移历史）。种子数据应通过独立的 Seed 脚本执行。

## Redis 部署 / Redis Deployment

```bash
# Docker
docker run -d --name redis -p 6379:6379 redis:7-alpine

# 生产建议：哨兵模式 / 集群模式 + 密码认证
docker run -d --name redis -p 6379:6379 redis:7-alpine \
  --requirepass YourPassword
```

Redis 不可用时系统自动降级：

| 功能 | Redis 可用 | Redis 不可用 |
|------|----------|-------------|
| 权限缓存 | < 2ms (hit) | ~5-10ms (DB query) |
| 登录频控 | IP + 用户名维度 | 仅 DB 维度的 Lockout |

## CORS 配置 / CORS Configuration

```jsonc
// 生产环境
"Cors": {
    "AllowedOrigins": [
        "https://admin.platformbase.com",
        "https://app.platformbase.com"
    ]
}
// 禁止使用 ["*"]
```

## 日志 / Logging

### 文件日志（当前）

```json
"WriteTo": [
    { "Name": "File", "Args": { "path": "logs/log-.txt", "rollingInterval": "Day", "retainedFileCountLimit": 30 } }
]
```

### 生产建议：集中式日志

支持 Serilog Sink 扩展：

- **Elasticsearch**: `Serilog.Sinks.Elasticsearch`
- **Seq**: `Serilog.Sinks.Seq`
- **Application Insights**: `Serilog.Sinks.ApplicationInsights`

```bash
dotnet add package Serilog.Sinks.Elasticsearch
```

```csharp
// Program.cs
config.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elk:9200")));
```

## 健康检查 / Health Checks

```bash
curl http://localhost:5269/health
# → {"success":true,"data":{"status":"Healthy"}}

# 生产环境可扩展健康检查：
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddRedis("localhost:6379");
```

## Docker 部署 / Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5269

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/PlatformBase.Host/PlatformBase.Host.csproj
RUN dotnet publish src/PlatformBase.Host/PlatformBase.Host.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=build /app .
COPY certs/identityserver.pfx /app/certs/
ENV ASPNETCORE_URLS=http://+:5269
ENTRYPOINT ["dotnet", "PlatformBase.Host.dll"]
```

```bash
docker build -t platformbase:latest .
docker run -d -p 5269:5269 \
  -e ConnectionStrings__DefaultConnection="Server=db;..." \
  -e Redis__ConnectionString="redis:6379" \
  --name platformbase platformbase:latest
```

## 故障排查 / Troubleshooting

### 401 Unauthorized

| 原因 | 解决方案 |
|------|---------|
| Token 过期 | 重新登录获取新 token |
| 签名密钥不匹配 | 检查 JwtBearer IssuerSigningKey 与 IdentityServer SigningCredential 是否同一把密钥 |
| Audience 不匹配 | token 中有无 `aud` claim？值是否为 `"api1"`？ |
| 旧进程未清理 | `lsof -ti:5269 \| xargs kill -9` |

### Swagger Authorize 后仍 401

| 原因 | 解决方案 |
|------|---------|
| swagger.json 中 security 为空 | 检查 `AddSecurityRequirement` 是否正确注册 |
| Authorization header 未附加 | 检查浏览器 DevTools Network 中 Request Headers |

### 数据库连接失败

| 原因 | 解决方案 |
|------|---------|
| SQLite 文件权限 | `chmod 666 app.db` |
| SQL Server 防火墙 | 开放 1433 端口 |
| MySQL 认证 | 检查 `AuthenticationPlugin` 是否为 `mysql_native_password` |
