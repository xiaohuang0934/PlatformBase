namespace PlatformBase.Application.Dtos.SystemParamModule;

/// <summary>
/// 更新系统参数请求
/// </summary>
public class UpdateSystemParamDto
{
    /// <summary>参数显示名称</summary>
    public string? Name { get; set; }

    /// <summary>参数值</summary>
    public string? Value { get; set; }

    /// <summary>分类分组</summary>
    public string? Category { get; set; }

    /// <summary>参数说明</summary>
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>排序号</summary>
    public int? SortOrder { get; set; }
}
