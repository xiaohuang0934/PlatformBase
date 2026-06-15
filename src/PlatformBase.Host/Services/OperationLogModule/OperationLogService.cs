using System.Linq.Expressions;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Extensions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services.OperationLogModule;

/// <summary>
/// 操作日志服务实现
/// 提供分页检索（按时间倒序、多维筛选）和异步写入（Hangfire 消费）
/// </summary>
public class OperationLogService : IOperationLogService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;

    public OperationLogService(IUnitOfWork uow, AppDbContext context)
    {
        _uow = uow;
        _context = context;
    }

    public async Task<PagedResult<OperationLogDto>> GetPagedAsync(
        OperationLogQuery query, CancellationToken ct = default)
    {
        var uname = query.Username?.Trim().ToUpperInvariant();
        var action = query.Action?.Trim();
        var kw = query.Keyword?.Trim().ToUpperInvariant();

        var filter = ((Expression<Func<OperationLog, bool>>?)null)
            .AppendIf(query.UserId.HasValue, l => l.UserId == query.UserId!.Value)
            .AppendIf(!string.IsNullOrWhiteSpace(action), l => l.Action == action)
            .AppendIf(!string.IsNullOrWhiteSpace(uname), l => l.Username != null && l.Username.ToUpper().Contains(uname!))
            .AppendIf(query.StartTime.HasValue, l => l.Timestamp >= query.StartTime!.Value)
            .AppendIf(query.EndTime.HasValue, l => l.Timestamp <= query.EndTime!.Value)
            .AppendIf(!string.IsNullOrWhiteSpace(kw), l =>
                (l.Username != null && l.Username.ToUpper().Contains(kw!))
                || (l.Action != null && l.Action.ToUpper().Contains(kw!))
                || (l.Detail != null && l.Detail.ToUpper().Contains(kw!)));

        var result = await _uow.Repository<OperationLog>().GetPagedAsync(new PagedRequest
        {
            PageIndex = query.PageIndex, PageSize = query.PageSize,
            SortField = nameof(OperationLog.Timestamp), IsAscending = false
        }, filter, ct);
        return new PagedResult<OperationLogDto>(
            result.TotalCount, result.PageIndex, result.PageSize,
            result.Items.Select(ToDto));
    }

    public async Task WriteAsync(OperationLogEntry entry, CancellationToken ct = default)
    {
        var log = new OperationLog
        {
            UserId = entry.UserId,
            Username = entry.Username,
            Action = entry.Action,
            Resource = entry.Resource,
            Detail = entry.Detail,
            IpAddress = entry.IpAddress,
            UserAgent = entry.UserAgent,
            IsSuccess = entry.IsSuccess,
            Timestamp = DateTime.UtcNow
        };

        _context.Set<OperationLog>().Add(log);
        await _uow.SaveChangesAsync(ct);
    }

    private static OperationLogDto ToDto(OperationLog entity) => new()
    {
        Id = entity.Id,
        Username = entity.Username,
        Action = entity.Action,
        Resource = entity.Resource,
        Detail = entity.Detail,
        IpAddress = entity.IpAddress,
        IsSuccess = entity.IsSuccess,
        Timestamp = entity.Timestamp
    };
}
