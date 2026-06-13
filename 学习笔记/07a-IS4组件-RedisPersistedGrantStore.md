# IS4 组件 — RedisPersistedGrantStore 完整实现

> **关联文档：** [07-JWT-IdentityServer4认证授权](./07-JWT-IdentityServer4认证授权.md) — 认证授权主文档，包含 ResourceOwnerPasswordValidator 和 ProfileService 的完整实现
>
> **依赖：** [13-Redis缓存](./13-Redis缓存.md) — Redis 缓存架构与降级策略

---

## 一、角色定义

`RedisPersistedGrantStore` 是 IdentityServer4 的 `IPersistedGrantStore` 接口的 Redis 实现。它负责持久化存储 **refresh_token**、**reference_token** 等授权凭证。

虽然是"持久化"，但数据存储在 Redis（内存）中，利用 Redis 的高并发读写能力，大幅提升令牌签发和验证的性能。

---

## 二、Key 设计

```
两层存储结构：

数据层:
  grant:{key}                    → String (JSON)
  例: grant:a1b2c3d4e5f6...      → {"Key":"...","Type":"refresh_token","SubjectId":"user-123",...}

索引层:
  grant:idx:{type}:{subjectId}   → Set
  例: grant:idx:refresh_token:user-123  → { "key1", "key2", "key3" }
```

**设计原因：** 如果只存数据不建索引，查询"某个用户的所有授权"需要 `SCAN` 全库性能极差。加一层 Set 索引后，按用户查询只需 `SMEMBERS + MGET` 两次命令。

---

## 三、性能对比

| 操作 | Redis 实现 | EF Core 实现 | 性能提升 |
|------|-----------|-------------|---------|
| Store（写入） | SET + SADD (1 pipeline) | INSERT (1 SQL) | 5-10x |
| Get（读取） | GET (<0.5ms) | SELECT by PK (~1ms) | 2-3x |
| GetAll（批量查询） | SMEMBERS + MGET (2 命令) | 2 表 JOIN (~5ms) | 5-10x |
| Remove（删除） | DEL + SREM (1 pipeline) | DELETE (2 SQL) | 5-10x |

---

## 四、完整实现

```csharp
using System.Text.Json;
using IdentityServer4.Models;
using StackExchange.Redis;

namespace PlatformBase.Host.IdentityServer;

/// <summary>
/// IdentityServer4 IPersistedGrantStore 的 Redis 实现（高并发场景）。
/// 将 refresh_token / reference_token 以 Redis 作为主存储，
/// 读写均为单次或少次 Redis 命令。
/// </summary>
public class RedisPersistedGrantStore : IdentityServer4.Stores.IPersistedGrantStore
{
    private readonly IDatabase _redis;                // Redis 实例（不可为空）
    private const string KeyPrefix = "grant:";        // 数据 Key 前缀
    private const string IdxPrefix = "grant:idx:";     // 索引 Set Key 前缀

    /// <summary>构造函数：注入 IConnectionMultiplexer 并获取默认数据库</summary>
    public RedisPersistedGrantStore(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();  // db=0
    }

    // ═══════════════════ StoreAsync — 写入授权 ═══════════════════
    public async Task StoreAsync(PersistedGrant grant)
    {
        var key = $"{KeyPrefix}{grant.Key}";        // grant:{授权Key}
        var ttl = grant.Expiration.HasValue         // TTL = Expiration - Now
            ? grant.Expiration.Value - DateTime.UtcNow
            : TimeSpan.FromDays(30);                // 无过期 → 默认 30 天

        if (ttl <= TimeSpan.Zero) return;           // 已过期 → 不存储

        // Pipeline 打包发送：减少网络往返
        var batch = _redis.CreateBatch();
        _ = batch.StringSetAsync(key, Serialize(grant), ttl);    // SET grant:{key} JSON TTL
        if (!string.IsNullOrEmpty(grant.SubjectId))               // 有用户 ID → 维护索引
            _ = batch.SetAddAsync(                                // SADD grant:idx:{type}:{subId} {key}
                $"{IdxPrefix}{grant.Type}:{grant.SubjectId}",
                grant.Key);
        batch.Execute();        // 一次性提交所有命令
        await Task.CompletedTask;
    }

    // ═══════════════════ GetAsync — 按 Key 读取 ═══════════════════
    public async Task<PersistedGrant?> GetAsync(string key)
    {
        var value = await _redis.StringGetAsync($"{KeyPrefix}{key}");   // GET grant:{key}
        return value.HasValue ? Deserialize(value!) : null;
    }

    // ═══════════════════ GetAllAsync — 按过滤条件批量查询 ═══════════════════
    public async Task<IEnumerable<PersistedGrant>> GetAllAsync(
        IdentityServer4.Stores.PersistedGrantFilter filter)
    {
        HashSet<string>? keys = null;

        // 按 SubjectId + Type 走索引（高效路径）
        if (!string.IsNullOrEmpty(filter.SubjectId))
        {
            if (filter.Type != null)
            {
                // 有明确 Type → 精准查单一索引集
                var members = await _redis.SetMembersAsync(
                    $"{IdxPrefix}{filter.Type}:{filter.SubjectId}");
                keys = members.Select(m => m.ToString()).ToHashSet();
            }
            else
            {
                // 无 Type → 遍历 IS4 的 4 种授权类型，合并所有索引集
                foreach (var grantType in new[] {
                    "refresh_token", "reference_token",
                    "authorization_code", "user_consent" })
                {
                    var members = await _redis.SetMembersAsync(
                        $"{IdxPrefix}{grantType}:{filter.SubjectId}");
                    keys ??= [];
                    foreach (var m in members) keys.Add(m.ToString());
                }
            }
        }

        if (keys == null) return [];    // 无 SubjectId → 不扫全库

        // MGET 批量获取
        var redisKeys = keys.Select(k =>
            new RedisKey($"{KeyPrefix}{k}")).ToArray();
        var values = await _redis.StringGetAsync(redisKeys);

        // 反序列化 + 客户端过滤 ClientId/SessionId
        var results = new List<PersistedGrant>();
        foreach (var val in values)
        {
            if (!val.HasValue) continue;
            var grant = Deserialize(val!);
            if (grant == null) continue;
            if (!string.IsNullOrEmpty(filter.ClientId)
                && grant.ClientId != filter.ClientId) continue;
            if (!string.IsNullOrEmpty(filter.SessionId)
                && grant.SessionId != filter.SessionId) continue;
            results.Add(grant);
        }

        return results;
    }

    // ═══════════════════ RemoveAsync — 删除单条 ═══════════════════
    public async Task RemoveAsync(string key)
    {
        var grant = await GetAsync(key);    // 先获取（需要 Type+SubjectId 清理索引）
        if (grant == null) return;

        var batch = _redis.CreateBatch();
        _ = batch.KeyDeleteAsync($"{KeyPrefix}{key}");       // DEL grant:{key}
        if (!string.IsNullOrEmpty(grant.SubjectId))
            _ = batch.SetRemoveAsync(                         // SREM grant:idx:{type}:{subId} {key}
                $"{IdxPrefix}{grant.Type}:{grant.SubjectId}", key);
        batch.Execute();
        await Task.CompletedTask;
    }

    // ═══════════════════ RemoveAllAsync — 批量删除 ═══════════════════
    public async Task RemoveAllAsync(
        IdentityServer4.Stores.PersistedGrantFilter filter)
    {
        var grants = await GetAllAsync(filter);     // 查匹配列表

        var batch = _redis.CreateBatch();           // Pipeline 批处理
        foreach (var grant in grants)
        {
            _ = batch.KeyDeleteAsync($"{KeyPrefix}{grant.Key}");
            if (!string.IsNullOrEmpty(grant.SubjectId))
                _ = batch.SetRemoveAsync(
                    $"{IdxPrefix}{grant.Type}:{grant.SubjectId}", grant.Key);
        }
        if (grants.Any()) batch.Execute();          // 有数据才提交
        await Task.CompletedTask;
    }

    /// <summary>撤销指定用户的所有授权（密码变更时调用）</summary>
    public async Task RevokeUserTokensAsync(string userId)
    {
        await RemoveAllAsync(new IdentityServer4.Stores.PersistedGrantFilter
        {
            SubjectId = userId
        });
    }

    // ═══════════════════ Serialize — 序列化 ═══════════════════
    private static string Serialize(PersistedGrant grant)
    {
        return JsonSerializer.Serialize(new
        {
            grant.Key, grant.Type, grant.SubjectId, grant.SessionId,
            grant.ClientId, grant.Description,
            grant.CreationTime, grant.Expiration,
            grant.ConsumedTime, grant.Data
        });
    }

    // ═══════════════════ Deserialize — 反序列化 ═══════════════════
    /// 使用 JsonDocument 逐字段解析（而非 Deserialize<PersistedGrant>()）
    /// 因为 PersistedGrant 不是简单 POCO，直接反序列化可能失败。
    private static PersistedGrant? Deserialize(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new PersistedGrant
            {
                Key = root.GetProperty("Key").GetString()!,
                Type = root.GetProperty("Type").GetString()!,
                SubjectId = root.TryGetProperty("SubjectId", out var s)
                    ? s.GetString() : null,
                SessionId = root.TryGetProperty("SessionId", out var si)
                    ? si.GetString() : null,
                ClientId = root.TryGetProperty("ClientId", out var c)
                    ? c.GetString() : null,
                Description = root.TryGetProperty("Description", out var d)
                    ? d.GetString() : null,
                CreationTime = root.TryGetProperty("CreationTime", out var ct)
                    ? ct.GetDateTime() : DateTime.UtcNow,
                Expiration = root.TryGetProperty("Expiration", out var ex)
                    && ex.ValueKind != JsonValueKind.Null
                    ? ex.GetDateTime() : null,
                ConsumedTime = root.TryGetProperty("ConsumedTime", out var co)
                    && co.ValueKind != JsonValueKind.Null
                    ? co.GetDateTime() : null,
                Data = root.TryGetProperty("Data", out var da)
                    ? da.GetString()! : string.Empty
            };
        }
        catch
        {
            return null;    // 解析失败 → 返回 null（数据损坏/版本不兼容）
        }
    }
}
```

---

## 五、Pipeline 批量提交原理

```
不使用 Pipeline（逐个发送）：
  Client ──SET key1 val1──▶ Redis
  Client ◀───────OK─────── Redis       ← RTT 1
  Client ──SADD set1 key1─▶ Redis
  Client ◀───────OK─────── Redis       ← RTT 2
  Client ──EXPIRE key1 3600▶ Redis
  Client ◀───────OK─────── Redis       ← RTT 3
  总计: 3 * RTT

使用 Pipeline（打包发送）：
  Client ──SET key1 val1──┐
  Client ──SADD set1 key1─┤───▶ Redis  (一次网络往返)
  Client ──EXPIRE key1 3600┘
  Client ◀─OK/OK/OK──────────── Redis
  总计: 1 * RTT  (性能提升 3 倍)
```

---

## 六、数据流向

```
StoreAsync:
  新 login/refresh → PersistedGrant 对象
    → Serialize(grant) → JSON 字符串
    → batch {SET grant:{key} JSON TTL, SADD idx:...}
    → batch.Execute() → Redis 写入

GetAsync:
  刷新 Token → IS4 需要验证旧 refresh_token
    → GetAsync(old_key)
    → GET grant:{key}
    → Deserialize(json) → PersistedGrant 对象

RemoveAllAsync:
  RevokeUserTokensAsync(userId) — 密码变更时
    → GetAllAsync({SubjectId: userId})
    → SMEMBERS grant:idx:*:userId → key 列表
    → MGET grant:{key1} grant:{key2} ...
    → batch {DEL grant:{key1}, DEL grant:{key2}, SREM ...}
    → batch.Execute()
```

---

## 七、与 EF Core 实现的对比

```csharp
// ❌ EF Core 实现（低性能）
public class EfPersistedGrantStore : IPersistedGrantStore
{
    public Task StoreAsync(PersistedGrant grant)
    {
        // INSERT INTO PersistedGrants ...  (1 次 DB 调用, ~3ms)
    }
    public Task<PersistedGrant> GetAsync(string key)
    {
        // SELECT * FROM PersistedGrants WHERE Key = @key  (~1ms)
    }
    public Task RemoveAsync(string key)
    {
        // DELETE FROM PersistedGrants WHERE Key = @key         (~3ms)
        // DELETE FROM PersistedGrants WHERE SubjectId = @sub  (需要额外清理索引)
    }
}

// ✅ Redis 实现（高性能）
public class RedisPersistedGrantStore : IPersistedGrantStore
{
    public async Task StoreAsync(PersistedGrant grant)
    {
        // SET + SADD (1 pipeline, <0.5ms)  → 快 5-10x
    }
    public async Task<PersistedGrant?> GetAsync(string key)
    {
        // GET (<0.3ms)  → 快 2-3x
    }
    public async Task RemoveAsync(string key)
    {
        // DEL + SREM (1 pipeline, <0.5ms)  → 快 5-10x
    }
}
```

---

## 八、最佳实践速查卡

```
┌─────────────────────────────────────────────────────────────────┐
│        RedisPersistedGrantStore 黄金法则                         │
├─────────────────────────────────────────────────────────────────┤
│  1. 双层存储：grant:{key} 数据 + grant:idx:{type}:{subId} 索引   │
│  2. 写操作用 Pipeline 批量提交（减少网络往返 RTT）                │
│  3. 无 SubjectId 的查询返回空（不 Scan 全库）                     │
│  4. Serialize 用匿名对象只保留核心字段（减少存储空间）            │
│  5. Deserialize 用 JsonDocument 逐字段解析（避免 POCO 反序列化    │
│     失败）                                                       │
│  6. 注入 IConnectionMultiplexer（非 IServiceProvider）— 不可降级   │
│  7. RevokeUserTokensAsync 密码变更时批量撤销所有授权              │
│  8. TTL = Expiration - Now，0 或负值跳过写入                     │
│  9. RemoveAllAsync 的 batch.Execute 只在有数据时提交             │
│ 10. 与 ResourceOwnerPasswordValidator / ProfileService 协作      │
│     完成完整的 OAuth2 令牌生命周期管理                            │
└─────────────────────────────────────────────────────────────────┘
```
