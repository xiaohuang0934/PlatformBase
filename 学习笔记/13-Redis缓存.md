# PlatformBase Redis 缓存 — 手把手教学笔记

---

## 一、Redis 在项目中的角色

Redis 在本项目中不是可选的"缓存加速"，而是**核心基础设施**，多处关键功能依赖它：

| 功能 | Redis 用途 | 不可用时行为 |
|------|-----------|-------------|
| 权限缓存 | 用户权限 (TTL=5min) | 降级查 DB |
| 系统参数缓存 | 配置项 (TTL=30min) | 降级查 DB |
| 数据字典缓存 | 字典项 (TTL=30min) | 降级查 DB |
| 登录频控 | 失败次数 + 锁定 | 跳过频控 |
| 安全戳 | stamp 校验 (TTL=180s) | 降级查 DB |
| API 限流 | 滑动窗口计数 | 跳过限流 |
| RefreshToken 存储 | IS4 PersistedGrant | **不降级**（不可用则 IS4 不工作） |
| 分布式锁 | 并发控制 | 跳过锁 |

**降级策略：** 除 IS4 PersistedGrant 外，所有 Redis 依赖都有 DB 兜底，保证"Redis 挂了系统仍可用"。

---

## 二、配置与注册

### 2.1 JSON 配置

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "DefaultDatabase": 0,
    "Enabled": true
  }
}
```

### 2.2 AddRedis 扩展

```csharp
public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration config)
{
    var enabled = config.GetValue<bool>("Redis:Enabled");

    if (!enabled)
    {
        return services;  // 关闭 Redis → 跳过注册，所有功能降级
    }

    try
    {
        var connString = config.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
        var muxer = ConnectionMultiplexer.Connect(connString);

        // 连接后 Ping 验证可用性
        muxer.GetDatabase().Ping();

        services.AddSingleton<IConnectionMultiplexer>(muxer);
    }
    catch
    {
        // Ping 失败 → Dispose，不注册 → 所有功能降级
    }

    return services;
}
```

**关键设计：**
- `Enabled=false` → 不尝试连接，所有 Redis 功能静默降级
- `Enabled=true` 但连接失败 → 同样降级，不阻塞应用启动
- 成功后注册单例 `IConnectionMultiplexer`

---

## 三、Redis 使用模式

### 3.1 模式 1：延迟获取 IDatabase（推荐）

```csharp
// 大多数服务使用此模式（容错性最好）
public class SomeService
{
    private readonly IDatabase? _redis;

    public SomeService(IServiceProvider sp)
    {
        _redis = sp.GetService<IConnectionMultiplexer>()?.GetDatabase();
        // 注意：GetService 不是 GetRequiredService → 不存在时返回 null
    }

    public async Task DoWorkAsync()
    {
        if (_redis == null) return;  // Redis 不可用 → 跳过

        var cached = await _redis.StringGetAsync("key");
        if (cached.HasValue) return cached.ToString();

        // 缓存未命中 → 查 DB
        var data = await QueryDatabase();
        await _redis.StringSetAsync("key", JsonSerializer.Serialize(data), TimeSpan.FromMinutes(30));
        return data;
    }
}
```

### 3.2 模式 2：直接注入 IConnectionMultiplexer（强依赖）

```csharp
// RedisPersistedGrantStore 使用此模式（不可降级）
public class RedisPersistedGrantStore : IPersistedGrantStore
{
    private readonly IDatabase _redis;  // 非 ? → 必须存在

    public RedisPersistedGrantStore(IConnectionMultiplexer redis)  // 非 nullable
    {
        _redis = redis.GetDatabase();
    }
}
```

### 3.3 模式 3：分布式锁

```csharp
public class RedisLockService : ILockService
{
    public async Task<bool> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken ct)
    {
        if (_redis == null) return true;  // Redis 不可用 → 不锁

        var token = Guid.NewGuid().ToString();
        return await _redis.StringSetAsync(
            $"lock:{key}",       // Key: lock:resource:123
            token,
            expiry,
            When.NotExists);     // SETNX 语义：仅当不存在时写入
    }

    public async Task ReleaseAsync(string key, CancellationToken ct)
    {
        if (_redis == null) return;
        await _redis.KeyDeleteAsync($"lock:{key}");
    }
}
```

---

## 四、项目中 Redis Key 一览

| Key 模式 | 使用处 | TTL | 类型 |
|----------|--------|-----|------|
| `user:perms:{userId}` | PermissionService | 5 分钟 | String (JSON) |
| `sysparam:{code}` | SystemParamService | 30 分钟 | String (JSON) |
| `dict:{typeCode}` | DataDictService | 30 分钟 | String (JSON) |
| `stamp:{userId}` | StampValidationMiddleware | 180 秒 | String |
| `login:fail:{user}` | ResourceOwnerPasswordValidator | 30 分钟 | String (计数) |
| `ratelimit:{ip}:{path}` | RateLimitFilter | seconds+1 | ZSet |
| `lock:{key}` | RedisLockService | 自定义 | String (SETNX) |
| `grant:{key}` | PersistedGrantStore | Expiration | String (JSON) |
| `grant:idx:{type}:{subId}` | PersistedGrantStore | 无 | Set |

---

## 五、权限缓存完整流程

```
请求: [Permission("users.create")]
  │
  ▼
PermissionAuthorizationHandler
  │
  ├─ Redis GET user:perms:{userId}
  │    ├─ HIT → 反序列化 → 判断 "users.create" ∈ perms? → 通过/拒绝
  │    │
  │    └─ MISS → DB 查询
  │              ├─ SELECT p.Code
  │              │  FROM UserPermissions up
  │              │  JOIN Permissions p ON ...
  │              │  WHERE up.UserId = {userId}
  │              │
  │              ├─ 序列化 → Redis SET user:perms:{userId} JSON EX 300
  │              │
  │              └─ 判断权限
  │
  ├─ Redis 异常 → 直接查 DB (降级，不缓存)
  └─ 权限变更时主动删除缓存 (RoleService/UserService)
```

---

## 六、缓存失效策略

```
写入: SystemParamService.SetValueAsync(code, value)
  │
  ├─ 更新 DB
  └─ Redis.KeyDelete($"sysparam:{code}")  // 主动删除缓存

读取: SystemParamService.GetValueAsync(code)
  │
  ├─ 查 Redis (cache-aside 模式)
  ├─ MISS → 查 DB → 写 Redis (TTL=30min)
  └─ HIT → 返回

结果: 更新后下一次读取自动回源，最多 30 分钟过期
```

---

## 七、架构全景图

```
┌─────────────────────────────────────────────────────────────────┐
│                      Redis 缓存架构                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  appsettings.json                                               │
│    Redis.Enabled = true/false                                    │
│    Redis.ConnectionString                                        │
│        │                                                        │
│        ▼                                                        │
│  AddRedis Extension                                              │
│    ├─ Enabled=false → 不注册 → 全局降级                         │
│    ├─ Connect + Ping 成功 → 注册 IConnectionMultiplexer Singleton│
│    └─ Connect/Ping 失败 → 不注册 → 全局降级                     │
│                                                                 │
│  消费方（8 个服务）:                                             │
│                                                                 │
│  ┌─ PermissionService ──── user:perms:{id}         String JSON  │
│  ├─ SystemParamService ─── sysparam:{code}         String JSON  │
│  ├─ DataDictService ────── dict:{typeCode}         String JSON  │
│  ├─ StampValMiddleware ─── stamp:{userId}          String       │
│  ├─ ResourceOwnerVal ───── login:fail:{user}       String       │
│  ├─ RateLimitFilter ────── ratelimit:{ip}:{path}   ZSet         │
│  ├─ RedisLockService ───── lock:{key}              String SETNX │
│  └─ PersistedGrantStore ── grant:{key} + idx       String + Set │
│                                                                 │
│  降级策略:                                                      │
│    所有读路径: Redis MISS → DB 兜底                              │
│    所有写路径: Redis 失败 → 跳过缓存，直接读写 DB                │
│    唯一不降级: PersistedGrantStore (IS4 强依赖)                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 八、最佳实践速查卡

```
┌─────────────────────────────────────────────────────────────────┐
│              Redis 缓存 黄金法则                                  │
├─────────────────────────────────────────────────────────────────┤
│  1. Enabled=false → 全局关闭 Redis，所有功能降级                 │
│  2. 连接时 Ping 验证可用性，失败时静默降级不阻塞启动              │
│  3. 所有缓存读 MISS 时回写（cache-aside 模式）                   │
│  4. 写操作主动删除缓存（Cache Invalidation），而非更新            │
│  5. 权限缓存 TTL=5min（平衡实时性和性能）                        │
│  6. 安全戳 TTL=180s（密码变更后最多 3 分钟旧 Token 失效）         │
│  7. IConnectionMultiplexer 注册为 Singleton（全应用共享一个连接）  │
│  8. IDatabase 通过 GetService<> 获取（nullable，支持降级）       │
│  9. 分布式锁用 SETNX（When.NotExists）保证原子性                 │
│ 10. 批量操作用 Batch/Pipeline 减少 RTT（PersistedGrantStore）     │
└─────────────────────────────────────────────────────────────────┘
```
