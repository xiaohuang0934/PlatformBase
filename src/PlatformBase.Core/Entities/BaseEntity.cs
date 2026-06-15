namespace PlatformBase.Core.Entities;

/// <summary>
/// 实体主键标识接口（泛型），定义实体的唯一标识
/// </summary>
/// <typeparam name="TKey">主键类型，必须为值类型</typeparam>
public interface IEntity<TKey> where TKey : struct
{
    /// <summary>实体主键</summary>
    TKey Id { get; set; }
}

/// <summary>
/// 实体主键标识接口（非泛型），便于通过反射批量获取实体类型
/// </summary>
public interface IEntity
{
}

/// <summary>
/// 实体基类（支持泛型主键类型）
/// </summary>
/// <typeparam name="TKey">主键类型，必须为值类型，推荐Guid、long、int</typeparam>
public abstract class BaseEntity<TKey> : IEntity<TKey>, IEntity where TKey : struct
{
    /// <summary>主键ID</summary>
    public TKey Id { get; set; }
}

/// <summary>
/// 实体基类（默认Guid主键，.NET企业级主流方案）
/// </summary>
public abstract class BaseEntity : BaseEntity<Guid>
{
}

/// <summary>
/// 审计标记接口，实现此接口的实体将自动记录创建/修改时间
/// </summary>
public interface IAuditable
{
    /// <summary>创建时间</summary>
    DateTime CreatedAt { get; set; }

    /// <summary>创建人用户ID</summary>
    Guid? CreatedBy { get; set; }

    /// <summary>最后修改时间</summary>
    DateTime? UpdatedAt { get; set; }

    /// <summary>最后修改人用户ID</summary>
    Guid? UpdatedBy { get; set; }
}

/// <summary>
/// 可审计实体基类，自动追踪创建和修改时间
/// </summary>
public abstract class AuditableEntity : BaseEntity, IAuditable
{
    /// <summary>创建时间，默认为UTC当前时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>创建人用户ID</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>最后修改时间</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>最后修改人用户ID</summary>
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// 软删除标记接口，全局查询过滤器将自动排除已标记删除的实体
/// </summary>
public interface ISoftDelete
{
    /// <summary>是否已删除</summary>
    bool IsDeleted { get; set; }

    /// <summary>删除时间</summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>删除操作用户ID</summary>
    Guid? DeletedBy { get; set; }
}

/// <summary>
/// 可软删除的审计实体基类，同时具备审计追踪和软删除功能
/// 软删除时自动记录删除时间和删除人
/// </summary>
public abstract class SoftDeleteEntity : AuditableEntity, ISoftDelete
{
    /// <summary>是否已删除，默认false</summary>
    public bool IsDeleted { get; set; }

    /// <summary>删除时间</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除操作用户ID</summary>
    public Guid? DeletedBy { get; set; }
}

// ═══════════════════ 多租户基类体系 ═══════════════════

/// <summary>
/// 租户感知标记接口，实现此接口的实体自动受全局 TenantId 过滤器保护
/// </summary>
public interface ITenantAware
{
    /// <summary>所属租户 ID</summary>
    Guid TenantId { get; set; }
}

/// <summary>可审计的租户实体基类</summary>
public abstract class TenantAuditableEntity : AuditableEntity, ITenantAware
{
    public Guid TenantId { get; set; }
}

/// <summary>可软删除的租户实体基类</summary>
public abstract class TenantSoftDeleteEntity : SoftDeleteEntity, ITenantAware
{
    public Guid TenantId { get; set; }
}

// ═══════════════════ 部门数据范围接口与基类 ═══════════════════

/// <summary>
/// 部门数据范围标记接口
/// 实现此接口的实体自动受 DataScopeFilter 过滤保护
/// </summary>
public interface IDataScopeAware
{
    /// <summary>所属部门 ID（null 表示未归属任何部门）</summary>
    Guid? OrganizationUnitId { get; set; }
}

/// <summary>
/// 部门级数据实体基类
/// 继承此类的实体自动具备：租户隔离 + 软删除 + 部门数据范围
/// </summary>
public abstract class DataScopeEntity : TenantSoftDeleteEntity, IDataScopeAware
{
    /// <summary>所属部门 ID（null 表示未归属任何部门，属于租户级公共数据）</summary>
    public Guid? OrganizationUnitId { get; set; }
}
