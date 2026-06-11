using System.ComponentModel.DataAnnotations;

namespace PlatformBase.Application.Dtos.PermissionModule;

/// <summary>
/// 创建权限请求
/// </summary>
public class CreatePermissionDto
{
    [Required(ErrorMessage = "权限编码不能为空")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "权限名称不能为空")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "接口路径不能为空")]
    public string ResourcePath { get; set; } = string.Empty;

    [Required(ErrorMessage = "HTTP方法不能为空")]
    [RegularExpression("^(GET|POST|PUT|DELETE|PATCH)$", ErrorMessage = "HTTP方法格式不正确")]
    public string HttpMethod { get; set; } = string.Empty;

    public string? GroupName { get; set; }

    public string? Description { get; set; }

    public int SortOrder { get; set; }
}
