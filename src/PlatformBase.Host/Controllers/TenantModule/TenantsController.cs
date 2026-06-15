using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Services;
using PlatformBase.Host.Authorization;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Controllers.TenantModule;

/// <summary>
/// 租户管理 + 平台账号分配 API
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _service;
    private readonly ICurrentUserContext _userContext;
    private readonly AppDbContext _db;

    public TenantsController(ITenantService service, ICurrentUserContext userContext, AppDbContext db)
    {
        _service = service;
        _userContext = userContext;
        _db = db;
    }

    /// <summary>分页查询租户列表，支持 keyword(搜索Name/Code/ContactEmail)、isEnabled筛选</summary>
    [HttpGet]
    [Permission("tenants.list")]
    public async Task<ApiResult<object>> GetPaged(
        [FromQuery] string? keyword, [FromQuery] bool? isEnabled,
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? sortField = null, [FromQuery] bool isAscending = true,
        CancellationToken ct = default)
    {
        var result = await _service.GetPagedAsync(keyword, isEnabled, pageIndex, pageSize, ct: ct);
        var dtos = result.Items.Select(t => new { t.Id, t.Name, t.Code, t.ContactEmail, t.IsEnabled, t.CreatedAt });
        return ApiResult<object>.Ok(new { result.TotalCount, result.PageIndex, result.PageSize, items = dtos });
    }

    /// <summary>查询租户详情</summary>
    [HttpGet("{id:guid}")]
    [Permission("tenants.list")]
    public async Task<ApiResult<object>> GetById(Guid id, CancellationToken ct)
    {
        var t = await _service.GetByIdAsync(id, ct);
        if (t == null) return ApiResult<object>.Fail(ErrorCode.DataNotFound, "租户不存在");
        return ApiResult<object>.Ok(new { t.Id, t.Name, t.Code, t.ContactEmail, t.IsEnabled, t.CreatedAt });
    }

    /// <summary>创建租户</summary>
    [HttpPost]
    [Permission("tenants.create")]
    public async Task<ApiResult<object>> Create([FromBody] TenantDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto.Name, dto.Code, dto.Email, ct);
        return ApiResult<object>.Ok(new { created.Id, created.Name, created.Code });
    }

    /// <summary>更新租户</summary>
    [HttpPut("{id:guid}")]
    [Permission("tenants.edit")]
    public async Task<ApiResult> Update(Guid id, [FromBody] TenantUpdateDto dto, CancellationToken ct)
    {
        await _service.UpdateAsync(id, dto.Name, dto.Email, ct);
        return ApiResult.Ok("更新成功");
    }

    /// <summary>停用租户</summary>
    [HttpDelete("{id:guid}")]
    [Permission("tenants.delete")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DisableAsync(id, ct);
        return ApiResult.Ok("已停用");
    }

    /// <summary>
    /// 获取当前平台管理员已分配的租户列表（前端下拉框数据源）。
    /// 非平台管理员返回空列表，租户用户返回空列表。
    /// </summary>
    [HttpGet("accessible")]
    public async Task<ApiResult<IReadOnlyList<object>>> GetAccessibleTenants(CancellationToken ct)
    {
        if (_userContext.UserType != UserType.PlatformAdmin)
            return ApiResult<IReadOnlyList<object>>.Ok([]);

        var ids = _userContext.TenantIds;
        if (ids.Count == 0)
            return ApiResult<IReadOnlyList<object>>.Ok([]);

        var tenants = await _db.Set<Tenant>()
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        return ApiResult<IReadOnlyList<object>>.Ok(tenants);
    }

    // ═══════════════════ 平台账号-租户关联管理 ═══════════════════

    /// <summary>查询租户已分配的平台账号</summary>
    [HttpGet("{tenantId:guid}/platform-users")]
    [Permission("tenants.edit")]
    public async Task<ApiResult<IReadOnlyList<Guid>>> GetPlatformUsers(Guid tenantId, CancellationToken ct)
    {
        var items = await _service.GetPlatformUserIdsForTenantAsync(tenantId, ct);
        return ApiResult<IReadOnlyList<Guid>>.Ok(items);
    }

    /// <summary>将平台账号分配到租户</summary>
    [HttpPost("{tenantId:guid}/platform-users/{userId:guid}")]
    [Permission("tenants.edit")]
    public async Task<ApiResult> AssignPlatformUser(Guid tenantId, Guid userId, CancellationToken ct)
    {
        await _service.AssignTenantToPlatformUserAsync(userId, tenantId, ct);
        return ApiResult.Ok("分配成功");
    }

    /// <summary>移除平台账号的租户关联</summary>
    [HttpDelete("{tenantId:guid}/platform-users/{userId:guid}")]
    [Permission("tenants.edit")]
    public async Task<ApiResult> RemovePlatformUser(Guid tenantId, Guid userId, CancellationToken ct)
    {
        await _service.RemoveTenantFromPlatformUserAsync(userId, tenantId, ct);
        return ApiResult.Ok("移除成功");
    }
}

public class TenantDto
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Code { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class TenantUpdateDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
}
