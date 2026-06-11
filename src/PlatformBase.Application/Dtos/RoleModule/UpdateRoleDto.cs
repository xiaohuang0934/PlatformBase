namespace PlatformBase.Application.Dtos.RoleModule;

/// <summary>
/// 更新角色请求
/// </summary>
public class UpdateRoleDto
{
    /// <summary>角色名称</summary>
    public string? Name { get; set; }

    /// <summary>角色描述</summary>
    public string? Description { get; set; }
}
