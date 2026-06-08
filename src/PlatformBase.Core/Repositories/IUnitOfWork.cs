using PlatformBase.Core.Entities;

namespace PlatformBase.Core.Repositories;

/// <summary>
/// 工作单元契约，管理数据库事务和仓储实例的生命周期
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 获取指定实体类型的仓储实例（惰性创建，同一次工作单元内复用）
    /// </summary>
    IRepository<T> Repository<T>() where T : BaseEntity;

    /// <summary>
    /// 获取指定泛型主键实体类型的仓储实例
    /// </summary>
    IKeyedRepository<T, TKey> KeyedRepository<T, TKey>() where T : BaseEntity<TKey> where TKey : struct;

    /// <summary>提交所有变更到数据库</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>开启数据库事务</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>提交事务</summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>回滚事务</summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
