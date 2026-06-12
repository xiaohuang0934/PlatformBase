using PlatformBase.Core.Entities;

namespace PlatformBase.Application.Dtos.UserModule;

/// <summary>
/// 用户列表/详情 DTO
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public UserType UserType { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
