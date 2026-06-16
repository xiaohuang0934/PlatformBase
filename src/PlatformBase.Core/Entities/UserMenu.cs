namespace PlatformBase.Core.Entities;

/// <summary>
/// 用户-菜单关联实体，用于控制用户可访问的菜单
/// 平台管理员和租户管理员可以为用户分配菜单
/// 用户只能看到已分配的菜单（平台管理员除外）
/// </summary>
public class UserMenu
{
    /// <summary>用户 ID</summary>
    public Guid UserId { get; set; }

    /// <summary>菜单 ID</summary>
    public Guid MenuId { get; set; }

    /// <summary>关联的用户</summary>
    public User User { get; set; } = null!;

    /// <summary>关联的菜单</summary>
    public Menu Menu { get; set; } = null!;
}
