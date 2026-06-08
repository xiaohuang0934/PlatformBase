using PlatformBase.Core.Entities;
using PlatformBase.Core.Models;

namespace PlatformBase.Application.Services;

/// <summary>
/// 通用CRUD应用服务契约，封装标准增删改查+分页的业务流程
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TDto">返回DTO类型</typeparam>
/// <typeparam name="TCreateDto">创建DTO类型</typeparam>
/// <typeparam name="TUpdateDto">更新DTO类型</typeparam>
public interface ICrudService<TEntity, TDto, TCreateDto, TUpdateDto>
    where TEntity : BaseEntity
{
    /// <summary>分页查询</summary>
    Task<PagedResult<TDto>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default);

    /// <summary>根据ID查询单条记录</summary>
    Task<TDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>新增记录</summary>
    Task<TDto> CreateAsync(TCreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>更新记录</summary>
    Task<TDto> UpdateAsync(Guid id, TUpdateDto dto, CancellationToken cancellationToken = default);

    /// <summary>删除记录</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
