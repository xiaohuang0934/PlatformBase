# 代码模式与小巧思 / Patterns & Tricks

## 1. Expression-based WhereIf（链式 AppendIf）

**灵感来源：** MemsApi 的 `IQueryable.WhereIf(bool, expression)` 链式过滤。

**核心问题：** 本项目不暴露 `IQueryable`，Service 通过 `IUnitOfWork.Repository` 操作数据。每个 `GetPagedAsync` 方法的过滤逻辑都需要手写 `if → 临时变量 → 声明表达式 → 手动合并`，大量重复。

**解决方案：** 造一个操作 `Expression<Func<T, bool>>` 的 AppendIf 版本，仅需 10 行。

```csharp
// Core/Extensions/ExpressionExtensions.cs
public static Expression<Func<T, bool>>? AppendIf<T>(
    this Expression<Func<T, bool>>? filter,
    bool condition,
    Expression<Func<T, bool>> predicate)
    => condition ? filter.Append(predicate) : filter;

public static Expression<Func<T, bool>>? Append<T>(
    this Expression<Func<T, bool>>? filter,
    Expression<Func<T, bool>> predicate)
    => filter == null ? predicate : filter.AndAlso(predicate);
```

**效果对比（UserService.GetPagedAsync）：**

```csharp
// 改前：25 行过滤逻辑
Expression<Func<User, bool>>? filter = null;
if (accessibleIds.Count > 0 && _currentUser.IsSuperAdmin)
{
    filter = u => u.TenantId == null || accessibleIds.Contains(u.TenantId.Value);
}
if (accessibleIds.Count > 0 && !_currentUser.IsSuperAdmin)
{
    var f = u => u.TenantId != null && accessibleIds.Contains(u.TenantId.Value);
    filter = filter == null ? f : filter.AndAlso(f);
}
// ...

// 改后：5 行链式调用
var filter = ((Expression<Func<User, bool>>?)null)
    .AppendIf(accessibleIds.Count > 0 && isAdmin,
        u => u.TenantId == null || accessibleIds.Contains(u.TenantId.Value))
    .AppendIf(accessibleIds.Count > 0 && !isAdmin,
        u => u.TenantId != null && accessibleIds.Contains(u.TenantId.Value))
    .AppendIf(query.IsActive.HasValue,
        u => u.IsActive == query.IsActive.Value)
    .AppendIf(!string.IsNullOrWhiteSpace(kw),
        u => u.NormalizedUsername.Contains(kw!));
```

**适用范围：** 本项目 5 个 Service 的 `GetPagedAsync` 全部使用，合计消除 ~105 行冗余代码。

**关键限制：** `Expression<Func<T,bool>>?` 不能直接使用 `?.` 链式调用 C# 的 null 条件运算符。需要使用 `((Expression<Func<T,bool>>?)null)` 起始 + 普通 `.` 调用。

---

## 2. Normalize 统一（消除 4 处重复）

**问题：** UserService、RoleService、AuthService、DataSeeder 中各有一份 `private static string Normalize(string) => (value ?? "").ToUpperInvariant()`。

**解决方案：** 提到 `Core/Extensions/StringExtensions.Normalize()`，一处定义，四处引用。

```csharp
// Core/Extensions/StringExtensions.cs
public static string Normalize(string? value) => (value ?? string.Empty).ToUpperInvariant();

// 使用侧
using PlatformBase.Core.Extensions;
var normalized = StringExtensions.Normalize(username);
```

---

## 3. 多租户自动填充（AppDbContext.ApplyAuditFields）

**问题：** 每个 `ITenantAware` 实体创建时都需要手动设置 `entity.TenantId`，容易被遗忘，导致数据写入 `Guid.Empty`。

**解决方案：** 在 `AppDbContext.SaveChangesAsync` 中统一处理——与 `CreatedBy` 同一个方法，自动填充。

```csharp
// AppDbContext.ApplyAuditFields()
var tenantId = _currentUserService.TenantId;
foreach (var entry in ChangeTracker.Entries<ITenantAware>())
{
    if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
    {
        entry.Entity.TenantId = tenantId ?? Guid.Empty;
    }
}
```

**效果：** 6 个模块（FileAttachment、OrganizationUnit、OperationLog、Notification、NotificationTemplate、Menu）无需在 Service 中手动设置 TenantId。平台管理员不选租户时写入 `Guid.Empty`，租户用户自动写入所属租户。

---

## 4. 缓存降级模式（Redis → DB Fallback）

**核心模式：** 所有 Redis 操作遵循统一降级规则——Redis 不可用时静默跳过，兜底到 DB。

```csharp
// 标准模式：三段式
private async Task<T?> TryGetValueAsync(string key)
{
    if (_redis == null) return default;     // ① Redis 未注册 → 跳过
    try { return await _redis.StringGet(key); }
    catch { return default; }               // ② Redis 不可用 → 跳过
}

private async Task TrySetValueAsync(string key, T value)
{
    if (_redis == null) return;
    try { await _redis.StringSet(key, value, TimeSpan.FromMinutes(30)); }
    catch { }  // ③ 写入失败不影响业务
}
```

**应用于：** SystemParamService、DataDictService、PermissionService、AuthService（频控）、RedisLockService。

**设计意图：** `catch { }` 不记录日志是刻意为之——Redis 降级是常态（开发环境无 Redis），非错误。

---

## 5. OperationLog 异步入队（Hangfire + ActionFilter）

**问题：** 操作日志写入不能阻塞 HTTP 响应。

**解决方案：** `IAsyncActionFilter` + Hangfire `BackgroundJob.Enqueue`。

```
HTTP 请求
  → Controller 执行 → OnActionExecuted
    → 提取操作信息（用户/Action/Resource/IP）
    → Hangfire.BackgroundJob.Enqueue<OperationLogWriterJob>
    → HTTP 响应立即返回（不等待日志写入）
                          ↓
            Hangfire Worker → WriteAsync → 写入 DB
```

**与同步写入对比：** 同步写入增加 10-50ms 响应延迟，异步入队几乎零开销。

---

## 6. 物化路径（Materialized Path）— 一次查询找所有子孙

**问题：** 查一个部门的所有子部门（含孙子部门），传统方式需要递归查询 N 次 DB。

**解决方案：** `OrganizationUnit.Path` 存储完整层级路径，用 LIKE 前缀匹配。

```sql
-- 数据
总公司  Path='/1/'
技术部  Path='/1/4/'
研发组  Path='/1/4/7/'

-- 查技术部及其所有子部门：1 次查询，零递归
WHERE Path LIKE '/1/4/%' OR Id = 4

-- Path 计算规则：父级 Path + 自己的 Id + '/'
```

**应用于：** `OrganizationUnit` 实体 + `DataScopeFilter` 数据权限（未来）。

**对比递归 CTE：**
- CTE：`WITH RECURSIVE ...` — SQLite 不支持
- 物化路径：`LIKE '/1/4/%'` — 全数据库兼容

---

## 7. 菜单权限裁剪（服务端判断）

**问题：** 不同用户看到的菜单不同，不能让前端拿到所有菜单后自行判断。

**解决方案：** 后端根据用户权限集合，遍历菜单树，自动裁剪无权限节点。

```csharp
// MenuService.GetUserMenuTreeAsync
if (_currentUser.IsSuperAdmin)
    return BuildTree(allMenus, null);  // 平台管理员 → 看全部

var grantedCodes = await _permService.GetUserPermissionCodesAsync(userId);
return BuildTree(allMenus, new HashSet<string>(grantedCodes));
```

**裁剪逻辑：** `BuildTree` 内部对每个节点判断 `PermissionCode`：
- `PermissionCode` 为空（目录/公共菜单）→ 可见
- `PermissionCode` 在用户权限集合中 → 可见
- 其他 → 不可见 → 从树中移除

**效果：** 前端只需调用 `GET /api/v1/menus/tree`，拿到直接渲染，不需要额外权限判断。

---

## 8. 角色权限分配 — 全量替换

**模式：** `AssignPermissionsAsync` 采用先清空再添加的全量替换，而非增量增删。

```csharp
// 全量替换
var existing = await _context.Set<RolePermission>().Where(rp => rp.RoleId == roleId).ToListAsync();
_context.Set<RolePermission>().RemoveRange(existing);  // ① 清空

foreach (var perm in permissions)
    _context.Set<RolePermission>().Add(new RolePermission { ... });  // ② 添加新

await _uow.SaveChangesAsync(ct);  // ③ 事务提交
```

**优于增量模式：**
- 前端传什么，后端就是什么——不会出现"前端漏传了移除操作"导致脏数据
- 事务保护：Remove + Add 在同一事务中，失败自动回滚
- 简单可靠：不需要前端传 `{ added: [...], removed: [...] }`

**同模式应用于：** User-Role 分配（`UserController.AssignRoles`）。

---

## 9. 三级变量解析（CurrentUserContext.TenantId）

**问题：** 平台管理员通过 X-Tenant-Id 头切换租户，但创建用户时不选租户应默认创建平台级用户。

**解决方案：** `UserController.Create` — 三级优先级赋值。

```csharp
user.TenantId = dto.TenantId              // ① 客户端传入（最高优先）
               ?? _currentUser.CurrentTenantId;   // ② 当前请求上下文（X-Tenant-Id）
                                           // ③ null = 平台管理员不选租户 → 平台级用户

user.UserType = user.TenantId == null ? UserType.PlatformAdmin : UserType.TenantUser;
```
