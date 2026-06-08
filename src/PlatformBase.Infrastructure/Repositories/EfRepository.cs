using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;

namespace PlatformBase.Infrastructure.Repositories;

/// <summary>
/// 基于Entity Framework Core的通用仓储实现
/// 提供标准CRUD + 分页查询（支持动态排序）+ 软删除
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public class EfRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DbContext Context;
    protected readonly DbSet<T> Set;

    public EfRepository(DbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    /// <summary>根据主键ID查询实体</summary>
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Set.FindAsync([id], cancellationToken);
    }

    /// <summary>根据条件查询第一条匹配记录</summary>
    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await Set.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <summary>查询所有记录</summary>
    public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Set.ToListAsync(cancellationToken);
    }

    /// <summary>根据条件查询记录列表</summary>
    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await Set.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 分页查询，支持按SortField动态排序
    /// 排序使用反射构建Expression，避免硬编码字段名与类型绑定
    /// </summary>
    public virtual async Task<PagedResult<T>> GetPagedAsync(PagedRequest request, Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        var query = Set.AsQueryable();

        if (filter != null)
        {
            query = query.Where(filter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // 动态排序：通过反射构建OrderBy/OrderByDescending表达式树
        if (!string.IsNullOrWhiteSpace(request.SortField))
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, request.SortField);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = request.IsAscending ? "OrderBy" : "OrderByDescending";
            var resultExpression = Expression.Call(
                typeof(Queryable), methodName,
                [typeof(T), property.Type],
                query.Expression, Expression.Quote(lambda));

            query = query.Provider.CreateQuery<T>(resultExpression);
        }

        var items = await query
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(totalCount, request.PageIndex, request.PageSize, items);
    }

    /// <summary>判断是否存在满足条件的记录</summary>
    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await Set.AnyAsync(predicate, cancellationToken);
    }

    /// <summary>统计记录数</summary>
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await Set.CountAsync(cancellationToken)
            : await Set.CountAsync(predicate, cancellationToken);
    }

    /// <summary>新增实体</summary>
    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await Set.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    /// <summary>批量新增</summary>
    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await Set.AddRangeAsync(entities, cancellationToken);
    }

    /// <summary>更新实体（全量更新）</summary>
    public virtual void Update(T entity)
    {
        Set.Update(entity);
    }

    /// <summary>批量更新</summary>
    public virtual void UpdateRange(IEnumerable<T> entities)
    {
        Set.UpdateRange(entities);
    }

    /// <summary>物理删除</summary>
    public virtual void Delete(T entity)
    {
        Set.Remove(entity);
    }

    /// <summary>批量物理删除</summary>
    public virtual void DeleteRange(IEnumerable<T> entities)
    {
        Set.RemoveRange(entities);
    }

    /// <summary>
    /// 软删除：将实体的IsDeleted标记为true，同时记录删除时间
    /// </summary>
    public virtual void SoftDelete(T entity)
    {
        if (entity is ISoftDelete softDeleteEntity)
        {
            softDeleteEntity.IsDeleted = true;
            softDeleteEntity.DeletedAt = DateTime.UtcNow;
            Update(entity);
        }
    }
}

/// <summary>
/// 泛型主键仓储实现，适用于非Guid主键的实体（如long、int）
/// </summary>
public class EfKeyedRepository<T, TKey> : IKeyedRepository<T, TKey>
    where T : BaseEntity<TKey>
    where TKey : struct
{
    protected readonly DbContext Context;
    protected readonly DbSet<T> Set;

    public EfKeyedRepository(DbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    /// <summary>根据泛型主键ID查询实体</summary>
    public virtual async Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await Set.FindAsync([id], cancellationToken);
    }

    /// <summary>根据条件查询第一条匹配记录</summary>
    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await Set.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <summary>查询所有记录</summary>
    public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Set.ToListAsync(cancellationToken);
    }

    /// <summary>根据条件查询记录列表</summary>
    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await Set.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>分页查询，支持动态排序</summary>
    public virtual async Task<PagedResult<T>> GetPagedAsync(PagedRequest request, Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        var query = Set.AsQueryable();

        if (filter != null)
        {
            query = query.Where(filter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.SortField))
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, request.SortField);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = request.IsAscending ? "OrderBy" : "OrderByDescending";
            var resultExpression = Expression.Call(
                typeof(Queryable), methodName,
                [typeof(T), property.Type],
                query.Expression, Expression.Quote(lambda));

            query = query.Provider.CreateQuery<T>(resultExpression);
        }

        var items = await query
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(totalCount, request.PageIndex, request.PageSize, items);
    }

    /// <summary>判断是否存在满足条件的记录</summary>
    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await Set.AnyAsync(predicate, cancellationToken);
    }

    /// <summary>统计记录数</summary>
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await Set.CountAsync(cancellationToken)
            : await Set.CountAsync(predicate, cancellationToken);
    }

    /// <summary>新增实体</summary>
    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await Set.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    /// <summary>批量新增</summary>
    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await Set.AddRangeAsync(entities, cancellationToken);
    }

    /// <summary>更新实体</summary>
    public virtual void Update(T entity)
    {
        Set.Update(entity);
    }

    /// <summary>批量更新</summary>
    public virtual void UpdateRange(IEnumerable<T> entities)
    {
        Set.UpdateRange(entities);
    }

    /// <summary>物理删除</summary>
    public virtual void Delete(T entity)
    {
        Set.Remove(entity);
    }

    /// <summary>批量物理删除</summary>
    public virtual void DeleteRange(IEnumerable<T> entities)
    {
        Set.RemoveRange(entities);
    }

    /// <summary>
    /// 软删除：将实体的IsDeleted标记为true，同时记录删除时间
    /// </summary>
    public virtual void SoftDelete(T entity)
    {
        if (entity is ISoftDelete softDeleteEntity)
        {
            softDeleteEntity.IsDeleted = true;
            softDeleteEntity.DeletedAt = DateTime.UtcNow;
            Update(entity);
        }
    }
}
