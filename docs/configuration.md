# 配置参考 / Configuration Reference

## appsettings.json 完整说明

```jsonc
{
  // ═══════════════════ 数据库 ═══════════════════
  "Database": {
    "Provider": "Sqlite",          // Sqlite | SqlServer | MySql
    "ConnectionString": "Data Source=app.db",
    "EnableSensitiveDataLogging": false  // 开发环境可开启查看 SQL
  },

  // ═══════════════════ JWT 认证 ═══════════════════
  "Jwt": {
    "Secret": "your-secret-key-min-32-characters-long",
    "Issuer": "http://localhost:5269",
    "Audience": "api1",
    "AccessTokenExpiration": 300    // 秒，默认 300（5 分钟）
  },

  // ═══════════════════ IdentityServer ═══════════════════
  "IdentityServer": {
    "Authority": "http://localhost:5269"  // 签发者地址
  },

  // ═══════════════════ Redis 缓存 ═══════════════════
  "Redis": {
    "Enabled": true,                // false = 完全跳过 Redis，降级到 DB
    "ConnectionString": "localhost:6379",
    "DefaultDatabase": 0
  },

  // ═══════════════════ 跨域 CORS ═══════════════════
  "Cors": {
    "AllowedOrigins": ["*"]         // 生产环境改为具体域名
  },

  // ═══════════════════ 文件存储 ═══════════════════
  "FileStorage": {
    "LocalPath": "uploads"          // 本地存储根目录（相对于应用根目录）
  },

  // ═══════════════════ Serilog 日志 ═══════════════════
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.EntityFrameworkCore": "Warning"  // 减少 EF Core 日志噪声
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

## 环境变量覆盖

所有配置均支持环境变量覆盖，格式为双下划线分隔：

```bash
export Database__Provider=SqlServer
export Database__ConnectionString="Server=.;Database=PlatformBase;..."
export Redis__Enabled=false
```

## 数据库切换

### SQLite（开发默认）

```jsonc
{
  "Database": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=app.db"
  }
}
```

### SQL Server

```jsonc
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=.;Database=PlatformBase;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

### MySQL

```jsonc
{
  "Database": {
    "Provider": "MySql",
    "ConnectionString": "Server=localhost;Database=PlatformBase;User=root;Password=123456;"
  }
}
```

## 配置文件层级

```
启动时加载顺序（后者覆盖前者）：
  ① appsettings.json
  ② appsettings.{Environment}.json  (Development / Production)
  ③ 环境变量
  ④ 命令行参数

示例：
  appsettings.json:           "Redis.Enabled": true
  appsettings.Production.json: "Redis.Enabled": true
  环境变量:                   Redis__Enabled=false
  → 最终生效:                 false
```

## 启动 Profile

`Properties/launchSettings.json`：

```jsonc
{
  "profiles": {
    "Development": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:5269",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

## 常见配置项组合

### 开发环境（无 Redis）

```jsonc
{
  "Database": { "Provider": "Sqlite", "ConnectionString": "Data Source=app.db" },
  "Redis": { "Enabled": false }
}
```

### 生产环境（完整配置）

```jsonc
{
  "Database": { "Provider": "SqlServer", "ConnectionString": "..." },
  "Redis": { "Enabled": true, "ConnectionString": "redis-cluster:6379" },
  "Cors": { "AllowedOrigins": ["https://your-app.com"] },
  "FileStorage": { "LocalPath": "/data/uploads" }
}
```
