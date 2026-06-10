using System.Security.Claims;
using PlatformBase.Core.Services;

namespace PlatformBase.Host.Services;

/// <summary>
/// 当前用户会话上下文实现
/// 从 <see cref="HttpContext.User"/>（由 JWT Bearer 中间件填充）中提取用户身份信息
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public Guid? UserId =>
        Guid.TryParse(
            User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    /// <inheritdoc />
    public string? UserName =>
        User?.FindFirstValue(ClaimTypes.Name);

    /// <inheritdoc />
    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email);

    /// <inheritdoc />
    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? [];

    /// <inheritdoc />
    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <inheritdoc />
    public string? ClientId =>
        User?.FindFirstValue("client_id");
}
