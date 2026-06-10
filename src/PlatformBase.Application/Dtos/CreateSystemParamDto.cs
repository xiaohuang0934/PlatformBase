namespace PlatformBase.Application.Dtos;

/// <summary>
/// 创建系统参数请求
/// </summary>
public class CreateSystemParamDto
{
    /// <summary>参数编码（全局唯一）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>参数显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>参数值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>分类分组</summary>
    public string? Category { get; set; }

    /// <summary>参数说明</summary>
    public string? Description { get; set; }

    /// <summary>排序号，默认 0</summary>
    public int SortOrder { get; set; }
}
