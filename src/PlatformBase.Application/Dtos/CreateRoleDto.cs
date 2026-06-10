namespace PlatformBase.Application.Dtos;

/// <summary>
/// 创建角色请求
/// </summary>
public class CreateRoleDto
{
    /// <summary>角色名称（全局唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色描述</summary>
    public string? Description { get; set; }
}
