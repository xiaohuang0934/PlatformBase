using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Host.Authorization;

namespace PlatformBase.Host.Controllers.OrganizationModule;

/// <summary>
/// 组织架构管理 API
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/organization-units")]
public class OrganizationUnitController : ControllerBase
{
    private readonly IOrganizationUnitService _service;

    public OrganizationUnitController(IOrganizationUnitService service) => _service = service;

    /// <summary>获取部门树形列表</summary>
    [HttpGet]
    [Permission("org-units.list")]
    public async Task<ApiResult<IReadOnlyList<OrgUnitNode>>> GetTree(CancellationToken ct)
        => ApiResult<IReadOnlyList<OrgUnitNode>>.Ok(await _service.GetTreeAsync(ct));

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
}

public class OrgUnitDto
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Code { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public int? SortOrder { get; set; }
}

public class OrgUnitUpdateDto
{
    public string? Name { get; set; }
    public Guid? ParentId { get; set; }
    public int? SortOrder { get; set; }
}
