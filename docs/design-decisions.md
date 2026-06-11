# 关键设计决策 / Design Decisions

## 架构层面

### 1. 仓储抽象 vs 直接 IQueryable

**决策：** Service 层通过 `IUnitOfWork.Repository<T>()` 操作数据，不直接暴露 `IQueryable`。

**理由：**
- Service 层不依赖 EF Core，换 ORM 或数据源只需换 `IRepository` 实现
- 分页/排序统一在 `IRepository.GetPagedAsync(PagedRequest, filter)` 中
- 缺点：每个查询条件需表达式树合并，代码稍冗长
- 优化：引入 `ExpressionExtensions.Append/AppendIf` 链式写法弥补

**备选方案：** 直接暴露 `AppDbContext.Set<T>()` 的 `IQueryable`，Service 层拼 LINQ。简洁但牺牲仓储抽象。

---

### 2. 多租户：单 TenantId vs M:N 平台账号映射

**决策：** `Users.TenantId`（nullable）+ `PlatformUserTenants` M:N 映射表。

**理由：**
- `TenantId=null` = 平台级用户（跨租户管理）
- `TenantId=guid` = 租户内用户
- 平台管理员通过 `PlatformUserTenants` 分配到特定租户，未分配不可见
- 优于单 `TenantId` 方案的"平台管理员看全部"——更精细的权限控制

**备选方案：** `User.TenantId` 单字段 + `UserType` 枚举区分。简单但无法支持"一个平台管理员只分配 N 个租户"的场景。

---

### 3. 租户参数：双表 vs 单表 + TenantId 区分

**决策：** `SystemParams`（全局）+ `TenantParams`（租户覆盖），双表 Fallback 查询。

**理由：**
- 安全隔离：租户 API 只暴露 TenantParams，永远不会误改全局 SystemParams
- 查询逻辑：先查 `TenantParams` → 未命中 → Fallback 到 `SystemParams`
- 缓存：`sysparam:{tenantId}:{code}`（租户级）+ `sysparam:global:{code}`（全局）
- 优于单表方案：不会出现租户误操作全局数据的安全风险

**备选方案：** 单表 `SystemParams`，`(Code, TenantId)` 组合唯一，`TenantId=NULL` 为全局默认。简单但租户可能误改全局值。

---

### 4. 组织架构：物化路径 vs 递归 CTE

**决策：** `OrganizationUnit.Path` 物化路径（如 `/1/4/7/`）。

**理由：**
- 查所有子部门：`WHERE Path LIKE '/1/4/%'` — 一次查询，零递归
- 跨数据库兼容（SQLite / MySQL / SQL Server）
- 代价：移动节点时需更新所有后代 Path（极低频操作）

**备选方案：**
- 递归 CTE：SQLite 不支持，MySQL 需 8.0+
- 闭包表：需维护额外的关联表，写入复杂
- 内存遍历：全表加载到内存，大数据量不可行

---

### 5. 事件总线：Channel vs RabbitMQ vs Redis Pub/Sub

**决策：** `System.Threading.Channels` 内存实现，通过 `IEventPublisher` 抽象预留 RabbitMQ 切换。

**理由：**
- 当前单体应用不需要外部队列，Channel 足够
- 性能：无锁设计，百万级 TPS
- 零外部依赖：不需要 RabbitMQ / Redis
- 切换路径：只需替换 `ChannelEventBus` → `RabbitMqEventBus`，Handler 层零改动

**备选方案：**
- RabbitMQ：外部依赖，部署复杂，当前场景过度设计
- Redis Pub/Sub：有 Redis 但无持久化，消息丢失不可恢复

---

### 6. API 统一响应：始终 HTTP 200

**决策：** 所有响应（含错误）统一 HTTP 200，错误信息在 `ApiResult` body 中。

**理由：**
- 客户端只需检查 `success` 和 `code`，不需要处理 HTTP 层错误
- `GlobalExceptionMiddleware` 捕获所有异常并转为 `ApiResult.Fail`
- `JwtBearerEvents.OnChallenge` / `OnForbidden` 也返回统一格式
- `StampValidationMiddleware` 返回 401 也走统一格式

**代价：** 日志中无法通过 HTTP 状态码快速识别错误（需看 body 的 `code` 字段）。

---

### 7. 权限鉴权：Code 匹配 vs Path 匹配

**决策：** `Permission.Code`（如 `users.list`）是权限判断的唯一依据，`Permission.ResourcePath` 仅为后台展示用。

**理由：**
- Code 是稳定的业务标识，不随路由变更而变化
- API 版本化管理后路由变化（`/api/users` → `/api/v1/users`），Code 不受影响
- `PermissionAuthorizationHandler` 通过 `HasPermissionAsync(userId, permissionCode)` 匹配

---

### 8. DI 注册：程序集扫描 + 手动注册

**决策：** `AddApplicationServices()` 自动扫描 `I*Service` → `*Service`，非标准接口手动注册。

**理由：**
- 标准 CRUD Service 无需手动注册，命名符合约定即可
- 非标准接口（`IFileStorageProvider`、`ILockService`、`IIdGenerator` 等）生命周期不同、命名不符合约定或接口不在 Application 层，保留手动注册
- `PersistedGrantStore` 无接口，必须手动注册

**备选方案：** 全部手动注册。Program.cs 会膨胀到 100+ 行。

---

## 数据层面

### 9. 密码哈希：BCrypt

**决策：** 使用 `BCrypt.Net-Next` 进行密码哈希存储。

**理由：**
- 自适应哈希（可配置 rounds），抵抗暴力破解
- 内建盐值，不额外管理 Salt 字段
- 比 SHA256/PBKDF2 更安全且足够快

---

### 10. 软删除：全局查询过滤器

**决策：** `SoftDeleteEntity.IsDeleted` + EF Core 全局查询过滤器自动排除。

**理由：**
- 所有 `IRepository.GetAllAsync/FindAsync/GetPagedAsync` 自动排除已删除记录
- 管理后台可通过 `IgnoreQueryFilters()` 查看已删除记录
- `AppDbContext.ApplyAuditFields` 自动填充 `DeletedAt/DeletedBy`

---

### 11. 主键策略：Guid

**决策：** 所有实体使用 `Guid` 主键（非自增 int/long）。

**理由：**
- 分布式友好：无需中心 ID 生成器
- 安全性：不暴露数据量（非连续）
- 合并冲突少：多环境数据合并时不需要重新映射
