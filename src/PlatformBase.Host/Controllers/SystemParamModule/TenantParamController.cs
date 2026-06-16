using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Services;
using PlatformBase.Host.Authorization;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Controllers.SystemParamModule;

/// <summary>
/// 租户参数管理 API
/// 平台管理员可按租户筛选，租户管理员只看自己的租户参数
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/tenant-params")]
public class TenantParamController : ControllerBase
{
    private readonly ISystemParamService _spService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserContext _currentUser;
    private readonly AppDbContext _db;

    public TenantParamController(ISystemParamService spService, IUnitOfWork uow, ICurrentUserContext currentUser, AppDbContext db)
    { _spService = spService; _uow = uow; _currentUser = currentUser; _db = db; }

    /// <summary>查询当前租户的参数覆盖值（TenantParam → Fallback SystemParam）</summary>
    [HttpGet("{code}")]
    [Permission("tenant-params.list")]
    public async Task<ApiResult<string?>> GetValue(string code, CancellationToken ct)
    {
        var tid = ResolveTenantId();
        if (tid == null) return ApiResult<string?>.Fail(ErrorCode.BadRequest, "请先选择租户");

        var tenantParam = await _uow.Repository<TenantParam>()
            .FirstOrDefaultAsync(p => p.TenantId == tid.Value && p.Code == code && p.IsEnabled, ct);
        if (tenantParam != null)
            return ApiResult<string?>.Ok(tenantParam.Value);

        var globalValue = await _spService.GetValueAsync(code, ct);
        return ApiResult<string?>.Ok(globalValue);
    }

    /// <summary>分页查询租户参数列表（按租户筛选）</summary>
    [HttpGet]
    [Permission("tenant-params.list")]
    public async Task<ApiResult<object>> GetPaged(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? keyword,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var tid = ResolveTenantId(tenantId);

        var query = from tp in _db.Set<TenantParam>()
                    join sp in _db.Set<SystemParam>() on tp.Code equals sp.Code into spGroup
                    from sp in spGroup.DefaultIfEmpty()
                    where tp.TenantId == tid
                    select new { tp, spValue = sp != null ? sp.Value : null };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToUpperInvariant();
            query = query.Where(x => x.tp.Code.ToUpper().Contains(kw) || x.tp.Name.ToUpper().Contains(kw));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.tp.SortOrder)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.tp.Id,
                x.tp.Code,
                x.tp.Name,
                x.tp.Value,
                x.tp.Category,
                x.tp.Description,
                x.tp.SortOrder,
                x.tp.IsEnabled,
                x.tp.CreatedAt,
                SystemParamValue = x.spValue
            })
            .ToListAsync(ct);

        return ApiResult<object>.Ok(new { totalCount = total, pageIndex, pageSize, items });
    }

    /// <summary>创建租户参数覆盖值（仅可覆盖 Inheritable=true 的系统参数）</summary>
    [HttpPost]
    [Permission("tenant-params.create")]
    public async Task<ApiResult> Create([FromBody] CreateSystemParamDto dto, [FromQuery] Guid? tenantId = null, CancellationToken ct = default)
    {
        var tid = ResolveTenantId(tenantId);
        if (tid == null) return ApiResult.Fail(ErrorCode.BadRequest, "请先选择租户");

        // 检查系统参数是否存在且可被继承
        var sysParam = await _uow.Repository<SystemParam>()
            .FirstOrDefaultAsync(p => p.Code == dto.Code && p.IsEnabled, ct);
        if (sysParam == null)
            throw new BusinessException($"系统参数 '{dto.Code}' 不存在", ErrorCode.DataNotFound);
        if (!sysParam.Inheritable)
            throw new BusinessException($"系统参数 '{dto.Code}' 不允许被租户覆盖", ErrorCode.Forbidden);

        var exists = await _uow.Repository<TenantParam>()
            .AnyAsync(p => p.TenantId == tid.Value && p.Code == dto.Code, ct);
        if (exists) throw new BusinessException("参数编码已存在", ErrorCode.DuplicateRecord);

        await _uow.Repository<TenantParam>().AddAsync(new TenantParam
        {
            TenantId = tid.Value, Code = dto.Code, Name = dto.Name ?? dto.Code, Value = dto.Value,
            Category = dto.Category ?? "custom", Description = dto.Description, SortOrder = dto.SortOrder
        }, ct);
        await _uow.SaveChangesAsync(ct);
        await _spService.InvalidateCacheAsync(dto.Code, ct);
        return ApiResult.Ok("创建成功");
    }

    /// <summary>更新租户参数覆盖值</summary>
    [HttpPut("{id:guid}")]
    [Permission("tenant-params.edit")]
    public async Task<ApiResult> Update(Guid id, [FromBody] UpdateSystemParamDto dto, CancellationToken ct)
    {
        var p = await _uow.Repository<TenantParam>().GetByIdAsync(id, ct);
        if (p == null) return ApiResult.Fail(ErrorCode.DataNotFound, "参数不存在");
        if (dto.Value != null) p.Value = dto.Value;
        if (dto.Description != null) p.Description = dto.Description;
        _uow.Repository<TenantParam>().Update(p);
        await _uow.SaveChangesAsync(ct);
        await _spService.InvalidateCacheAsync(p.Code, ct);
        return ApiResult.Ok("更新成功");
    }

    /// <summary>删除租户参数覆盖值</summary>
    [HttpDelete("{id:guid}")]
    [Permission("tenant-params.delete")]
    public async Task<ApiResult> Delete(Guid id, CancellationToken ct)
    {
        var p = await _uow.Repository<TenantParam>().GetByIdAsync(id, ct);
        if (p == null) return ApiResult.Fail(ErrorCode.DataNotFound, "参数不存在");
        _uow.Repository<TenantParam>().Delete(p);
        await _uow.SaveChangesAsync(ct);
        await _spService.InvalidateCacheAsync(p.Code, ct);
        return ApiResult.Ok("删除成功");
    }

    /// <summary>解析目标租户 ID</summary>
    private Guid? ResolveTenantId(Guid? requestTenantId = null)
    {
        // 平台管理员：使用请求中指定的租户或当前视角租户
        if (_currentUser.UserType == UserType.PlatformAdmin)
            return requestTenantId ?? _currentUser.CurrentTenantId;

        // 租户用户：固定为当前租户
        return _currentUser.TenantId;
    }
}
