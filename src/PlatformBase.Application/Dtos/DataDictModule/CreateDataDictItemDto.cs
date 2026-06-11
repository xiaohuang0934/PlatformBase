using System.ComponentModel.DataAnnotations;

namespace PlatformBase.Application.Dtos.DataDictModule;

/// <summary>
/// 创建数据字典项请求
/// </summary>
public class CreateDataDictItemDto
{
    [Required(ErrorMessage = "字典类型ID不能为空")]
    public Guid DictTypeId { get; set; }

    [Required(ErrorMessage = "项编码不能为空")]
    public string ItemCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "项名称不能为空")]
    public string ItemName { get; set; } = string.Empty;

    public string? ItemValue { get; set; }

    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }
}
