namespace PlatformBase.Core.Entities;

/// <summary>
/// API 接口权限定义实体
/// 每个权限对应一个具体的 API 端点，包含路径、HTTP 方法和权限编码
/// </summary>
public class Permission : AuditableEntity
{
    /// <summary>权限编码，全局唯一（如：users.create / roles.delete）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>权限名称，用于管理后台展示（如："创建用户"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>API 接口路径（如：/api/users）</summary>
    public string ResourcePath { get; set; } = string.Empty;

    /// <summary>HTTP 请求方法（GET / POST / PUT / DELETE）</summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>展示分组标签（如："用户管理"、"系统管理"），仅用于管理后台归类展示</summary>
    public string? GroupName { get; set; }

    /// <summary>排序号，用于管理后台展示时的顺序控制</summary>
    public int SortOrder { get; set; }

    /// <summary>是否启用，停用后鉴权自动跳过该权限点</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>权限描述备注</summary>
    public string? Description { get; set; }
}
