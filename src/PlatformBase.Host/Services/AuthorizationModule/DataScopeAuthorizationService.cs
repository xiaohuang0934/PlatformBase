using Microsoft.EntityFrameworkCore;
using PlatformBase.Core.Entities;
using PlatformBase.Core.Exceptions;
using PlatformBase.Core.Services;
using PlatformBase.Infrastructure.Data;

namespace PlatformBase.Host.Services.AuthorizationModule;

/// <summary>
/// 数据权限授权服务实现
/// 集中处理用户类型校验、租户覆盖、部门/角色归属验证
/// </summary>
public class DataScopeAuthorizationService : IDataScopeAuthorizationService
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IOrganizationUnitService _orgService;
    private readonly AppDbContext _dbContext;

    public DataScopeAuthorizationService(
        ICurrentUserContext currentUser,
        IOrganizationUnitService orgService,
        AppDbContext dbContext)
    {
        _currentUser = currentUser;
        _orgService = orgService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<DataScopeAuthResult> AuthorizeAsync(
        IReadOnlyList<UserType> bannedUserTypes,
        Guid? requestTenantId,
        UserType? requestUserType,
        List<Guid>? requestOrgIds,
        List<Guid>? requestRoleIds,
        CancellationToken cancellationToken = default)
    {
        // 1. 检查是否在禁止列表中
        if (bannedUserTypes.Count > 0 && bannedUserTypes.Contains(_currentUser.UserType))
        {
            return new DataScopeAuthResult
            {
                IsBanned = true,
                BanReason = "当前用户类型无权执行此操作"
            };
        }

        // 2. 根据用户层级确定目标租户和用户类型
        var (targetTenantId, targetUserType) = ResolveTenantAndUserType(requestTenantId, requestUserType);

        // 3. 校验部门归属
        if (requestOrgIds is { Count: > 0 })
            await ValidateOrganizationUnitsAsync(requestOrgIds, targetTenantId, cancellationToken);

        // 4. 校验角色归属
        if (requestRoleIds is { Count: > 0 })
            await ValidateRolesAsync(requestRoleIds, targetTenantId, cancellationToken);

        return new DataScopeAuthResult
        {
            IsBanned = false,
            TenantId = targetTenantId,
            UserType = targetUserType
        };
    }

    /// <summary>
    /// 根据用户层级确定目标租户和用户类型
    /// 平台管理员：必须指定租户，可指定用户类型
    /// 租户管理员：强制当前租户，只能创建租户用户
    /// </summary>
    private (Guid? tenantId, UserType userType) ResolveTenantAndUserType(
        Guid? requestTenantId, UserType? requestUserType)
    {
        // 平台管理员（含超级管理员）
        if (_currentUser.UserType == UserType.PlatformAdmin)
        {
            if (requestTenantId == null)
                throw new BusinessException("平台管理员操作必须指定租户ID", ErrorCode.TenantIdRequired);

            if (!_currentUser.TenantIds.Contains(requestTenantId.Value))
                throw new BusinessException($"无权访问租户: {requestTenantId}", ErrorCode.TenantAccessDenied);

            if (requestUserType == UserType.PlatformAdmin)
                throw new BusinessException("不能创建或修改为平台管理员", ErrorCode.CannotCreatePlatformAdmin);

            var userType = requestUserType ?? UserType.TenantUser;
            return (requestTenantId, userType);
        }

        // 租户管理员
        if (_currentUser.UserType == UserType.TenantAdmin)
        {
            var tenantId = _currentUser.TenantId;
            if (tenantId == null)
                throw new BusinessException("租户管理员必须有所属租户", ErrorCode.TenantAccessDenied);

            if (requestUserType != null && requestUserType != UserType.TenantUser)
                throw new BusinessException("租户管理员只能创建或修改为租户普通用户", ErrorCode.CanOnlyCreateTenantUser);

            return (tenantId, UserType.TenantUser);
        }

        throw new BusinessException("无权执行此操作", ErrorCode.NoPermissionToOperate);
    }

    /// <summary>
    /// 校验部门是否属于目标租户
    /// </summary>
    private async Task ValidateOrganizationUnitsAsync(
        List<Guid> orgIds, Guid? targetTenantId, CancellationToken ct)
    {
        var orgs = await _orgService.GetByIdsAsync(orgIds, ct);

        if (orgs.Count != orgIds.Count)
            throw new BusinessException("部分部门不存在", ErrorCode.DataNotFound);

        var invalidOrgs = orgs.Where(o => o.TenantId != targetTenantId).ToList();
        if (invalidOrgs.Count > 0)
            throw new BusinessException("部门不属于目标租户", ErrorCode.OrganizationNotInTenant);
    }

    /// <summary>
    /// 校验角色是否属于目标租户（全局角色 TenantId=null 对所有租户可用）
    /// </summary>
    private async Task ValidateRolesAsync(
        List<Guid> roleIds, Guid? targetTenantId, CancellationToken ct)
    {
        var roles = await _dbContext.Set<Role>()
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(ct);

        if (roles.Count != roleIds.Count)
            throw new BusinessException("部分角色不存在", ErrorCode.DataNotFound);

        var invalidRoles = roles.Where(r => r.TenantId != null && r.TenantId != targetTenantId).ToList();
        if (invalidRoles.Count > 0)
            throw new BusinessException("角色不属于目标租户", ErrorCode.RoleNotInTenant);
    }
}