using PlatformBase.Application.Services;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Repositories;

namespace PlatformBase.Host.Services;

/// <summary>
/// 组织架构服务实现（物化路径）
/// </summary>
public class OrganizationUnitService : IOrganizationUnitService
{
    private readonly IUnitOfWork _uow;

    public OrganizationUnitService(IUnitOfWork uow) => _uow = uow;

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
}
