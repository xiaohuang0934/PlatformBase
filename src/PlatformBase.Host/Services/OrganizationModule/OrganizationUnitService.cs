using Microsoft.EntityFrameworkCore;
using PlatformBase.Application.Dtos.UserModule;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Models;
using PlatformBase.Core.Repositories;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services.OrganizationModule;

/// <summary>
/// 组织架构服务实现（物化路径）
/// </summary>
public class OrganizationUnitService : IOrganizationUnitService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;
    private readonly ICurrentUserContext _currentUser;

    public OrganizationUnitService(IUnitOfWork uow, AppDbContext context, ICurrentUserContext currentUser)
    {
        _uow = uow;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<OrgUnitNode>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await _uow.Repository<OrganizationUnit>()
            .FindAsync(o => o.IsEnabled, ct);
        return BuildTree(all);
    }

    public async Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _uow.Repository<OrganizationUnit>().GetByIdAsync(id, ct);

    public async Task<OrganizationUnit> CreateAsync(string name, string code, Guid? parentId, int sortOrder,
        CancellationToken ct = default)
    {
        if (await _uow.Repository<OrganizationUnit>().AnyAsync(o => o.Code == code, ct))
            throw new BusinessException("部门编码已存在", ErrorCode.DuplicateRecord);

        // 校验父部门归属（必须属于同一租户）
        if (parentId != null)
        {
            var parent = await GetByIdAsync(parentId.Value, ct);
            if (parent == null)
                throw new BusinessException("父部门不存在", ErrorCode.DataNotFound);

            if (_currentUser.TenantId != null && parent.TenantId != _currentUser.TenantId)
                throw new BusinessException("父部门不属于当前租户", ErrorCode.ParentOrgNotInTenant);
        }

        var entity = new OrganizationUnit { Name = name, Code = code, ParentId = parentId, SortOrder = sortOrder };
        var created = await _uow.Repository<OrganizationUnit>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        var parentPath = parentId != null
            ? (await GetByIdAsync(parentId.Value, ct))?.Path ?? "/"
            : "/";
        created.Path = $"{parentPath}{created.Id}/";
        _uow.Repository<OrganizationUnit>().Update(created);
        await _uow.SaveChangesAsync(ct);
        return created;
    }

    public async Task<OrganizationUnit> UpdateAsync(Guid id, string? name, Guid? parentId, int? sortOrder,
        CancellationToken ct = default)
    {
        var entity = await _uow.Repository<OrganizationUnit>().GetByIdAsync(id, ct)
            ?? throw new BusinessException("部门不存在", ErrorCode.DataNotFound);
        if (name != null) entity.Name = name;
        if (parentId.HasValue) entity.ParentId = parentId;
        if (sortOrder.HasValue) entity.SortOrder = sortOrder.Value;
        _uow.Repository<OrganizationUnit>().Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<OrganizationUnit>().GetByIdAsync(id, ct)
            ?? throw new BusinessException("部门不存在", ErrorCode.DataNotFound);
        _uow.Repository<OrganizationUnit>().SoftDelete(entity);
        await _uow.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<OrganizationUnit>> GetByIdsAsync(List<Guid> ids, CancellationToken ct = default)
    {
        if (ids == null || ids.Count == 0) return [];
        return await _uow.Repository<OrganizationUnit>()
            .FindAsync(o => ids.Contains(o.Id), ct);
    }

    private static List<OrgUnitNode> BuildTree(List<OrganizationUnit> all)
    {
        var map = all.ToDictionary(o => o.Id, o => new OrgUnitNode
        {
            Id = o.Id, Name = o.Name, Code = o.Code, ParentId = o.ParentId, SortOrder = o.SortOrder
        });
        var roots = new List<OrgUnitNode>();
        foreach (var o in all.OrderBy(o => o.SortOrder))
        {
            var node = map[o.Id];
            if (o.ParentId != null && map.TryGetValue(o.ParentId.Value, out var parent))
                parent.Children.Add(node);
            else roots.Add(node);
        }
        return roots;
    }

    // ═══════════════════ 部门用户查询 ═══════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> GetUsersAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var userIds = await _context.Set<UserOrganizationUnit>()
            .Where(uo => uo.OrganizationUnitId == organizationUnitId)
            .Select(uo => uo.UserId)
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0) return [];

        var users = await _uow.Repository<User>()
            .FindAsync(u => userIds.Contains(u.Id) && u.IsActive, cancellationToken);

        return users;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> GetUsersWithChildrenAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        // 获取当前部门的物化路径
        var org = await GetByIdAsync(organizationUnitId, cancellationToken);
        if (org == null) return [];

        // 查询当前部门及所有子级部门（Path LIKE '{org.Path}%')
        var orgIds = await _uow.Repository<OrganizationUnit>()
            .FindAsync(o => o.Path.StartsWith(org.Path) && o.IsEnabled, cancellationToken);

        var orgIdList = orgIds.Select(o => o.Id).ToList();

        if (orgIdList.Count == 0) return [];

        // 查询这些部门下的所有用户
        var userIds = await _context.Set<UserOrganizationUnit>()
            .Where(uo => orgIdList.Contains(uo.OrganizationUnitId))
            .Select(uo => uo.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0) return [];

        var users = await _uow.Repository<User>()
            .FindAsync(u => userIds.Contains(u.Id) && u.IsActive, cancellationToken);

        return users;
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserDto>> GetUsersPagedAsync(Guid organizationUnitId, UserQuery query, CancellationToken cancellationToken = default)
    {
        // 获取当前部门的物化路径
        var org = await GetByIdAsync(organizationUnitId, cancellationToken);
        if (org == null)
            return new PagedResult<UserDto>(0, query.PageIndex, query.PageSize, []);

        // 查询当前部门及所有子级部门
        var orgIds = await _uow.Repository<OrganizationUnit>()
            .FindAsync(o => o.Path.StartsWith(org.Path) && o.IsEnabled, cancellationToken);

        var orgIdList = orgIds.Select(o => o.Id).ToList();

        if (orgIdList.Count == 0)
            return new PagedResult<UserDto>(0, query.PageIndex, query.PageSize, []);

        // 查询这些部门下的用户 ID
        var userIds = await _context.Set<UserOrganizationUnit>()
            .Where(uo => orgIdList.Contains(uo.OrganizationUnitId))
            .Select(uo => uo.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
            return new PagedResult<UserDto>(0, query.PageIndex, query.PageSize, []);

        // 分页查询用户
        var kw = query.Keyword?.Trim().ToUpperInvariant();
        var result = await _uow.Repository<User>().GetPagedAsync(new PagedRequest
        {
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            SortField = query.SortField ?? nameof(User.Username),
            IsAscending = query.IsAscending
        }, u =>
            userIds.Contains(u.Id) &&
            u.IsActive &&
            (kw == null || u.NormalizedUsername.Contains(kw) || (u.NormalizedEmail != null && u.NormalizedEmail.Contains(kw))),
            cancellationToken);

        var dtos = result.Items.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            EmailConfirmed = u.EmailConfirmed,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive,
            UserType = u.UserType,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();

        return new PagedResult<UserDto>(result.TotalCount, result.PageIndex, result.PageSize, dtos);
    }
}
