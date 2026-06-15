using Microsoft.EntityFrameworkCore.Storage;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Repositories;

namespace PlatformBase.Infrastructure.Data;

/// <summary>
/// 工作单元实现，封装DbContext事务管理和仓储实例的惰性创建
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取指定实体类型对应的仓储实例（默认Guid主键），同一实体类型在同一工作单元内复用同一实例
    /// </summary>
    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            _repositories[type] = new EfRepository<T>(_context);
        }
        return (IRepository<T>)_repositories[type];
    }

    /// <summary>
    /// 获取指定泛型主键实体类型的仓储实例
    /// </summary>
    public IKeyedRepository<T, TKey> KeyedRepository<T, TKey>() where T : BaseEntity<TKey> where TKey : struct
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            _repositories[type] = new EfKeyedRepository<T, TKey>(_context);
        }
        return (IKeyedRepository<T, TKey>)_repositories[type];
    }

    /// <summary>将所有变更持久化到数据库</summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>开启数据库事务，用于跨多个仓储操作的一致性</summary>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>提交当前事务</summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>回滚当前事务</summary>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }
        await _context.DisposeAsync();
    }
}
