using System.ComponentModel.DataAnnotations;

namespace PlatformBase.Application.Dtos;

/// <summary>
/// 创建数据字典类型请求
/// </summary>
public class CreateDataDictTypeDto
{
    [Required(ErrorMessage = "字典类型编码不能为空")]
    public string TypeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "字典类型名称不能为空")]
    public string TypeName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }
}
