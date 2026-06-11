using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos;
using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers.MenuModule;

/// <summary>
/// 菜单管理 API
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/menus")]
public class MenuController : ControllerBase
{
    private readonly IMenuService _service;

    public MenuController(IMenuService service) => _service = service;

    /// <summary>获取当前用户可访问的菜单树（登录即可）</summary>
    [HttpGet("tree")]
    [Authorize]
    public async Task<ApiResult<IReadOnlyList<MenuNode>>> GetUserTree(CancellationToken ct)
    {
        var tree = await _service.GetUserMenuTreeAsync(ct);
        return ApiResult<IReadOnlyList<MenuNode>>.Ok(tree);
    }

    /// <summary>全部菜单列表</summary>
    [HttpGet]
    [Permission("menus.list")]
    public async Task<ApiResult<IReadOnlyList<MenuDto>>> GetAll(CancellationToken ct)
    {
        var items = await _service.GetAllAsync(ct);
        var dtos = items.Select(ToDto).ToList();
        return ApiResult<IReadOnlyList<MenuDto>>.Ok(dtos);
    }

    /// <summary>菜单详情</summary>
    [HttpGet("{id:guid}")]
    [Permission("menus.list")]
    public async Task<ApiResult<MenuDto>> GetById(Guid id, CancellationToken ct)
    {
        var m = await _service.GetByIdAsync(id, ct);
        if (m == null) return ApiResult<MenuDto>.Fail(ErrorCode.DataNotFound, "菜单不存在");
        return ApiResult<MenuDto>.Ok(ToDto(m));
    }

    /// <summary>创建菜单</summary>
    [HttpPost]
    [Permission("menus.create")]
    public async Task<ApiResult<MenuDto>> Create([FromBody] CreateMenuDto dto, CancellationToken ct)
    {
        var menu = new Menu
        {
            Name = dto.Name, Type = dto.Type, ParentId = dto.ParentId,
            Path = dto.Path, Component = dto.Component, Icon = dto.Icon,
            PermissionCode = dto.PermissionCode, SortOrder = dto.SortOrder,
            IsVisible = dto.IsVisible, KeepAlive = dto.KeepAlive
        };
        var created = await _service.CreateAsync(menu, ct);
        return ApiResult<MenuDto>.Ok(ToDto(created));
    }

    /// <summary>更新菜单</summary>
    [HttpPut("{id:guid}")]
    [Permission("menus.edit")]
    public async Task<ApiResult<MenuDto>> Update(Guid id, [FromBody] UpdateMenuDto dto, CancellationToken ct)
    {
        var existing = await _service.GetByIdAsync(id, ct);
        if (existing == null) return ApiResult<MenuDto>.Fail(ErrorCode.DataNotFound, "菜单不存在");

        if (dto.Name != null) existing.Name = dto.Name;
        if (dto.Type.HasValue) existing.Type = dto.Type.Value;
        if (dto.ParentId != null) existing.ParentId = dto.ParentId;
        if (dto.Path != null) existing.Path = dto.Path;
        if (dto.Component != null) existing.Component = dto.Component;
        if (dto.Icon != null) existing.Icon = dto.Icon;
        if (dto.PermissionCode != null) existing.PermissionCode = dto.PermissionCode;
        if (dto.SortOrder.HasValue) existing.SortOrder = dto.SortOrder.Value;
        if (dto.IsVisible.HasValue) existing.IsVisible = dto.IsVisible.Value;
        if (dto.IsEnabled.HasValue) existing.IsEnabled = dto.IsEnabled.Value;
        if (dto.KeepAlive.HasValue) existing.KeepAlive = dto.KeepAlive.Value;

        var updated = await _service.UpdateAsync(existing, ct);
        return ApiResult<MenuDto>.Ok(ToDto(updated));
    }

    /// <summary>删除菜单（软删除）</summary>
    [HttpDelete("{id:guid}")]
    [Permission("menus.delete")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return ApiResult.Ok("删除成功");
    }

    private static MenuDto ToDto(Menu entity) => new()
    {
        Id = entity.Id, Name = entity.Name, Type = entity.Type, ParentId = entity.ParentId,
        Path = entity.Path, Component = entity.Component, Icon = entity.Icon,
        PermissionCode = entity.PermissionCode, SortOrder = entity.SortOrder,
        IsVisible = entity.IsVisible, IsEnabled = entity.IsEnabled, KeepAlive = entity.KeepAlive
    };
}
