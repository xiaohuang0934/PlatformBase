using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Application.Dtos.UserModule;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Services;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers.OrganizationModule;

/// <summary>
/// 组织架构管理 API（懒加载模式）
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/organization-units")]
public class OrganizationUnitController : ControllerBase
{
    private readonly IOrganizationUnitService _service;
    private readonly ICurrentUserContext _currentUser;

    public OrganizationUnitController(IOrganizationUnitService service, ICurrentUserContext currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 懒加载获取部门数据
    /// - 无参数 → 返回租户摘要列表（顶层节点）
    /// - tenantId 指定 → 返回该租户的一级部门或指定父级的子部门
    /// - mode=tree → 返回全量部门树（用于 OrgSelector 等组件）
    /// </summary>
    [HttpGet]
    [Permission("org-units.list")]
    public async Task<ApiResult<IReadOnlyList<object>>> GetTree(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? parentId,
        [FromQuery] string? mode,
        CancellationToken ct)
    {
        // 租户管理员只能查看本租户部门
        if (_currentUser.UserType != UserType.PlatformAdmin && tenantId.HasValue && _currentUser.TenantId.HasValue && tenantId.Value != _currentUser.TenantId.Value)
            return ApiResult<IReadOnlyList<object>>.Fail(ErrorCode.Forbidden, "无权访问该租户的部门");

        if (mode == "tree")
        {
            var tree = await _service.GetFullTreeAsync(ct);
            return ApiResult<IReadOnlyList<object>>.Ok(tree.Cast<object>().ToList());
        }

        if (tenantId == null && parentId == null)
        {
            var summaries = await _service.GetTenantSummariesAsync(ct);
            return ApiResult<IReadOnlyList<object>>.Ok(summaries.Cast<object>().ToList());
        }

        var nodes = await _service.GetTreeAsync(tenantId, parentId, ct);
        return ApiResult<IReadOnlyList<object>>.Ok(nodes.Cast<object>().ToList());
    }

    /// <summary>查询部门详情（含物化路径）</summary>
    [HttpGet("{id:guid}")]
    [Permission("org-units.list")]
    public async Task<ApiResult<object>> GetById(Guid id, CancellationToken ct)
    {
        var o = await _service.GetByIdAsync(id, ct);
        if (o == null) return ApiResult<object>.Fail(ErrorCode.DataNotFound, "部门不存在");
        return ApiResult<object>.Ok(new { o.Id, o.Name, o.Code, o.ParentId, o.SortOrder, o.IsEnabled, o.Path });
    }

    /// <summary>创建部门（自动计算物化路径）</summary>
    [HttpPost]
    [Permission("org-units.create")]
    public async Task<ApiResult<object>> Create([FromBody] OrgUnitDto dto, CancellationToken ct)
    {
        // 平台管理员指定租户时，切换当前租户视角
        if (dto.TenantId != null && _currentUser.UserType == UserType.PlatformAdmin)
            _currentUser.SetCurrentTenant(dto.TenantId);

        var created = await _service.CreateAsync(dto.Name, dto.Code, dto.ParentId, dto.SortOrder ?? 0, ct);
        return ApiResult<object>.Ok(new { created.Id, created.Name, created.Code, created.Path });
    }

    /// <summary>更新部门</summary>
    [HttpPut("{id:guid}")]
    [Permission("org-units.edit")]
    public async Task<ApiResult> Update(Guid id, [FromBody] OrgUnitUpdateDto dto, CancellationToken ct)
    {
        await _service.UpdateAsync(id, dto.Name, dto.ParentId, dto.SortOrder, ct);
        return ApiResult.Ok("更新成功");
    }

    /// <summary>删除部门（软删除）</summary>
    [HttpDelete("{id:guid}")]
    [Permission("org-units.delete")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return ApiResult.Ok("删除成功");
    }

    // ═══════════════════ 部门用户查询 ═══════════════════

    /// <summary>查询部门下的用户（仅本部门）</summary>
    [HttpGet("{id:guid}/users")]
    [Permission("org-units.list")]
    public async Task<ApiResult<IReadOnlyList<User>>> GetUsers(Guid id, CancellationToken ct)
    {
        var users = await _service.GetUsersAsync(id, ct);
        return ApiResult<IReadOnlyList<User>>.Ok(users);
    }

    /// <summary>分页查询部门及其子级部门的用户</summary>
    [HttpGet("{id:guid}/users/with-children")]
    [Permission("org-units.list")]
    public async Task<ApiResult<PagedResult<UserDto>>> GetUsersWithChildren(
        Guid id, [FromQuery] UserQuery query, CancellationToken ct)
    {
        var result = await _service.GetUsersPagedAsync(id, query, ct);
        return ApiResult<PagedResult<UserDto>>.Ok(result);
    }
}

public class OrgUnitDto
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Code { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? ParentId { get; set; }
    public int? SortOrder { get; set; }
}

public class OrgUnitUpdateDto
{
    public string? Name { get; set; }
    public Guid? ParentId { get; set; }
    public int? SortOrder { get; set; }
}
