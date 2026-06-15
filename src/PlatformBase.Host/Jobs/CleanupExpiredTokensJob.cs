using Microsoft.EntityFrameworkCore;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Jobs;

/// <summary>
/// 过期 RefreshToken 清理任务
/// 每天凌晨 3 点执行，清理 30 天前过期的 PersistedGrant 记录，回写执行结果
/// </summary>
public class CleanupExpiredTokensJob : IRecurringJob
{
    private readonly AppDbContext _context;
    private readonly IJobScheduleService _scheduleService;

    public CleanupExpiredTokensJob(AppDbContext context, IJobScheduleService scheduleService)
    {
        _context = context;
        _scheduleService = scheduleService;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-30);
            var expired = await _context.PersistedGrants
                .Where(g => g.Expiration < cutoff)
                .ToListAsync(cancellationToken);

            _context.PersistedGrants.RemoveRange(expired);
            await _context.SaveChangesAsync(cancellationToken);

            await _scheduleService.UpdateRunResultAsync(
                "cleanup-expired-tokens", DateTime.UtcNow, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await _scheduleService.UpdateRunResultAsync(
                "cleanup-expired-tokens", DateTime.UtcNow, ex.Message, cancellationToken);
            throw;
        }
    }
}
