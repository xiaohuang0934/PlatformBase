namespace PlatformBase.Core.Entities;

/// <summary>
/// 系统参数实体，提供运行时 Key-Value 配置中心
/// 支持分类分组、功能开关、Redis 缓存降级
/// 继承 <see cref="SoftDeleteEntity"/> 获得审计追踪 + 软删除能力
/// </summary>
public class SystemParam : SoftDeleteEntity
{
    /// <summary>参数编码，全局唯一（如 max_login_attempts）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>参数显示名称（如 "最大登录失败次数"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>参数值（统一 string 存储，业务层按需类型转换）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>分类分组（如 general / security / feature-toggle）</summary>
    public string? Category { get; set; }

    /// <summary>参数说明</summary>
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>同类内排序</summary>
    public int SortOrder { get; set; }
}
