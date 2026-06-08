using System.Linq.Expressions;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Models;

namespace PlatformBase.Core.Repositories;

/// <summary>
/// 通用仓储契约（默认Guid主键），提供标准的CRUD和分页查询方法
/// </summary>
/// <typeparam name="T">实体类型，必须继承BaseEntity（默认Guid主键）</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>根据主键ID查询实体</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>根据条件查询第一条匹配记录</summary>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>查询所有记录（慎用大表）</summary>
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>根据条件查询列表</summary>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>分页查询，支持动态排序和过滤</summary>
    Task<PagedResult<T>> GetPagedAsync(PagedRequest request, Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>判断是否存在满足条件的记录</summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>统计满足条件的记录数</summary>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>新增实体，返回入库后的实体</summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>批量新增</summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>更新实体</summary>
    void Update(T entity);

    /// <summary>批量更新</summary>
    void UpdateRange(IEnumerable<T> entities);

    /// <summary>物理删除实体</summary>
    void Delete(T entity);

    /// <summary>批量物理删除</summary>
    void DeleteRange(IEnumerable<T> entities);

    /// <summary>软删除：将实体的IsDeleted标记为true</summary>
    void SoftDelete(T entity);
}

/// <summary>
/// 泛型主键仓储契约，适用于非Guid主键的实体（如long、int）
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IKeyedRepository<T, TKey> where T : BaseEntity<TKey> where TKey : struct
{
    /// <summary>根据泛型主键ID查询实体</summary>
    Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>根据条件查询第一条匹配记录</summary>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>查询所有记录（慎用大表）</summary>
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>根据条件查询列表</summary>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>分页查询，支持动态排序和过滤</summary>
    Task<PagedResult<T>> GetPagedAsync(PagedRequest request, Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>判断是否存在满足条件的记录</summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>统计满足条件的记录数</summary>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>新增实体，返回入库后的实体</summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>批量新增</summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>更新实体</summary>
    void Update(T entity);

    /// <summary>批量更新</summary>
    void UpdateRange(IEnumerable<T> entities);

    /// <summary>物理删除实体</summary>
    void Delete(T entity);

    /// <summary>批量物理删除</summary>
    void DeleteRange(IEnumerable<T> entities);

    /// <summary>软删除：将实体的IsDeleted标记为true</summary>
    void SoftDelete(T entity);
}
