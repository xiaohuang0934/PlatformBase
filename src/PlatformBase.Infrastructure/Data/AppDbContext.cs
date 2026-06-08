using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using PlatformBase.Core.Entities;

namespace PlatformBase.Infrastructure.Data;

/// <summary>
/// 应用程序数据库上下文
/// 自动处理：软删除全局查询过滤、审计字段自动填充
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 为所有实现ISoftDelete的实体自动配置全局查询过滤器，排除已软删除的记录
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(ConvertFilterExpression<ISoftDelete>(e => !e.IsDeleted, entityType.ClrType));
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

    /// <summary>遍历ChangeTracker，为新建/修改的IAuditable实体自动设置时间戳</summary>
    private void ApplyAuditFields()
    {
        var entries = ChangeTracker.Entries<IAuditable>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
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
}
