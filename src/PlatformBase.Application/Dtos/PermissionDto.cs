namespace PlatformBase.Application.Dtos;

/// <summary>
/// 权限信息返回 DTO，用于管理后台权限列表展示
/// </summary>
public class PermissionDto
{
    /// <summary>权限 ID</summary>
    public Guid Id { get; set; }

    /// <summary>权限编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>权限名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>API 接口路径</summary>
    public string ResourcePath { get; set; } = string.Empty;

    /// <summary>HTTP 请求方法</summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>展示分组标签</summary>
    public string? GroupName { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>描述备注</summary>
    public string? Description { get; set; }
}
