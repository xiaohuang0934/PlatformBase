using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Services;

namespace PlatformBase.Infrastructure.Data;

/// <summary>
/// 应用程序数据库上下文
/// 自动处理：软删除全局查询过滤、审计字段自动填充（含操作人）
/// 通过 <see cref="ICurrentUserService"/> 注入当前用户，自动填充 CreatedBy / UpdatedBy / DeletedBy
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// 构造函数，由 DI 容器注入 DbContext 配置和当前用户上下文
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    // ═══════════════════ 认证授权相关实体 ═══════════════════

    /// <summary>用户表</summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>角色表</summary>
    public DbSet<Role> Roles { get; set; } = null!;

    /// <summary>用户-角色关联表</summary>
    public DbSet<UserRole> UserRoles { get; set; } = null!;

    /// <summary>API 权限定义表</summary>
    public DbSet<Permission> Permissions { get; set; } = null!;

    /// <summary>角色-权限关联表</summary>
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;

    /// <summary>用户直达权限表（含 IsGranted 覆盖）</summary>
    public DbSet<UserPermission> UserPermissions { get; set; } = null!;

    /// <summary>IdentityServer4 持久化授权表（refresh_token 等）</summary>
    public DbSet<PersistedGrantEntity> PersistedGrants { get; set; } = null!;

    // ═══════════════════ 基础业务实体 ═══════════════════

    /// <summary>系统参数表（运行时 Key-Value 配置）</summary>
    public DbSet<SystemParam> SystemParams { get; set; } = null!;

    /// <summary>数据字典类型表</summary>
    public DbSet<DataDictType> DataDictTypes { get; set; } = null!;

    /// <summary>数据字典项表（含层级 ParentId）</summary>
    public DbSet<DataDictItem> DataDictItems { get; set; } = null!;

    /// <summary>任务调度配置表</summary>
    public DbSet<JobSchedule> JobSchedules { get; set; } = null!;

    /// <summary>操作日志表</summary>
    public DbSet<OperationLog> OperationLogs { get; set; } = null!;

    /// <summary>文件附件表</summary>
    public DbSet<FileAttachment> FileAttachments { get; set; } = null!;

    /// <summary>通知模板表</summary>
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;

    /// <summary>通知表</summary>
    public DbSet<Notification> Notifications { get; set; } = null!;

    // ═══════════════════ 多租户实体 ═══════════════════

    /// <summary>租户表</summary>
    public DbSet<Tenant> Tenants { get; set; } = null!;

    /// <summary>平台账号-租户映射表</summary>
    public DbSet<PlatformUserTenant> PlatformUserTenants { get; set; } = null!;

    /// <summary>租户参数表</summary>
    public DbSet<TenantParam> TenantParams { get; set; } = null!;

    /// <summary>租户字典类型表</summary>
    public DbSet<TenantDataDictType> TenantDataDictTypes { get; set; } = null!;

    /// <summary>租户字典项表</summary>
    public DbSet<TenantDataDictItem> TenantDataDictItems { get; set; } = null!;

    /// <summary>组织架构表</summary>
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; } = null!;

    /// <summary>菜单表</summary>
    public DbSet<Menu> Menus { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ───── 认证授权实体配置 ─────
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.NormalizedUsername);
            e.HasIndex(u => u.NormalizedEmail);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
            e.HasIndex(r => r.NormalizedName);
        });

        modelBuilder.Entity<UserRole>(e =>
            e.HasKey(ur => new { ur.UserId, ur.RoleId }));

        modelBuilder.Entity<RolePermission>(e =>
            e.HasKey(rp => new { rp.RoleId, rp.PermissionId }));

        modelBuilder.Entity<UserPermission>(e =>
            e.HasKey(up => new { up.UserId, up.PermissionId }));

        modelBuilder.Entity<Permission>(e =>
            e.HasIndex(p => p.Code).IsUnique());

        // ───── 基础业务实体配置 ─────
        modelBuilder.Entity<SystemParam>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.HasIndex(p => p.Category);
        });

        modelBuilder.Entity<DataDictType>(e =>
        {
            e.HasIndex(t => t.TypeCode).IsUnique();
        });

        modelBuilder.Entity<DataDictItem>(e =>
        {
            e.HasIndex(i => new { i.DictTypeId, i.ItemCode }).IsUnique();
            e.HasIndex(i => i.DictTypeId);
            e.HasIndex(i => i.ParentId);
            e.HasOne(i => i.DictType)
             .WithMany(t => t.Items)
             .HasForeignKey(i => i.DictTypeId);
            e.HasOne(i => i.Parent)
             .WithMany(i => i.Children)
             .HasForeignKey(i => i.ParentId);
        });

        modelBuilder.Entity<JobSchedule>(e =>
        {
            e.HasIndex(j => j.JobId).IsUnique();
        });

        modelBuilder.Entity<OperationLog>(e =>
        {
            e.HasIndex(l => l.UserId);
            e.HasIndex(l => l.Action);
            e.HasIndex(l => l.Timestamp);
        });

        modelBuilder.Entity<FileAttachment>(e =>
        {
            e.HasIndex(f => new { f.BizType, f.BizId });
            e.HasIndex(f => f.Bucket);
        });

        modelBuilder.Entity<NotificationTemplate>(e =>
        {
            e.HasIndex(t => t.Code).IsUnique();
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasIndex(n => n.UserId);
            e.HasIndex(n => new { n.UserId, n.IsRead });
            e.HasIndex(n => n.Timestamp);
        });

        modelBuilder.Entity<PersistedGrantEntity>(e =>
        {
            e.HasKey(pg => pg.Key);
            e.HasIndex(pg => pg.SubjectId);
            e.HasIndex(pg => pg.ClientId);
            e.HasIndex(pg => pg.Expiration);
        });

        // ───── 多租户实体配置 ─────
        modelBuilder.Entity<Tenant>(e => e.HasIndex(t => t.Code).IsUnique());
        modelBuilder.Entity<PlatformUserTenant>(e =>
            e.HasKey(p => new { p.PlatformUserId, p.TenantId }));
        modelBuilder.Entity<TenantParam>(e =>
            e.HasIndex(p => new { p.TenantId, p.Code }).IsUnique());
        modelBuilder.Entity<TenantDataDictType>(e =>
            e.HasIndex(t => new { t.TenantId, t.TypeCode }).IsUnique());
        modelBuilder.Entity<TenantDataDictItem>(e =>
        {
            e.HasIndex(i => new { i.TenantId, i.DictTypeId, i.ItemCode }).IsUnique();
            e.HasIndex(i => i.DictTypeId);
            e.HasIndex(i => i.ParentId);
        });

        modelBuilder.Entity<OrganizationUnit>(e =>
        {
            e.HasIndex(o => o.Code);
            e.HasIndex(o => o.ParentId);
            e.HasIndex(o => o.Path);
        });

        modelBuilder.Entity<Menu>(e =>
        {
            e.HasIndex(m => m.ParentId);
            e.HasIndex(m => m.PermissionCode);
        });

        // ───── 多租户全局查询过滤器（ITenantAware 实体的数据隔离）─────
        ApplyTenantFilters(modelBuilder);

        // ───── 软删除全局查询过滤器（排除已软删除的记录） ─────
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(ConvertFilterExpression<ISoftDelete>(
                        e => !e.IsDeleted, entityType.ClrType));
            }
        }
    }

    /// <summary>保存时自动填充审计字段</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>保存时自动填充审计字段</summary>
    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// 遍历 ChangeTracker，自动填充审计字段
    /// <list type="bullet">
    ///   <item>EntityState.Added     → CreatedAt / CreatedBy（当前用户ID）</item>
    ///   <item>EntityState.Modified  → UpdatedAt / UpdatedBy（当前用户ID）</item>
    ///   <item>ISoftDelete 软删除   → DeletedAt / DeletedBy（当前用户ID）</item>
    /// </list>
    /// CreatedBy / UpdatedBy / DeletedBy 的值从 <see cref="ICurrentUserService.UserId"/> 获取，
    /// 未登录时填 null
    /// </summary>
    private void ApplyAuditFields()
    {
        var currentUserId = _currentUserService.UserId;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.CreatedBy ??= currentUserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedBy ??= currentUserId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State == EntityState.Modified && entry.Entity.IsDeleted)
            {
                entry.Entity.DeletedAt ??= DateTime.UtcNow;
                entry.Entity.DeletedBy ??= currentUserId;
            }
        }

        // 多租户：自动填充 TenantId
        var tenantId = _currentUserService.TenantId;
        foreach (var entry in ChangeTracker.Entries<ITenantAware>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantId ?? Guid.Empty;
            }
        }
    }

    /// <summary>
    /// 将接口类型的Lambda表达式转换为实体类型的Lambda表达式，
    /// 用于HasQueryFilter注册全局查询过滤器
    /// </summary>
    private static LambdaExpression ConvertFilterExpression<TInterface>(
        Expression<Func<TInterface, bool>> filterExpression, Type entityType)
    {
        var param = Expression.Parameter(entityType);
        var body = ReplacingExpressionVisitor.Replace(
            filterExpression.Parameters.Single(), param, filterExpression.Body);

        return Expression.Lambda(body, param);
    }

    /// <summary>
    /// 多租户全局查询过滤器
    /// 根据 ICurrentUserService.AccessibleTenantIds 控制可见范围：
    /// - 租户用户 → 只能看自己租户的数据
    /// - 平台管理员（已分配）→ 可看已分配租户 + Guid.Empty（通用数据）
    /// - 无分配记录 → 什么也看不到
    /// </summary>
    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantAware).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(BuildTenantFilter(entityType.ClrType));
            }
        }
    }

    private LambdaExpression BuildTenantFilter(Type entityType)
    {
        // ⚠️ 关键：不在模型构建时捕获 AccessibleTenantIds 的副本，
        // 而是在查询时通过 _currentUserService 实时求值
        var param = Expression.Parameter(entityType, "e");

        Expression<Func<ITenantAware, bool>> filter = e =>
            _currentUserService.AccessibleTenantIds.Count == 0
                ? false
                : e.TenantId == Guid.Empty || _currentUserService.AccessibleTenantIds.Contains(e.TenantId);

        return ConvertFilterExpression(filter, entityType);
    }
}
