using FiapGames.Shared.Kernel.Entities;
using FiapGames.Shared.Kernel.Pagination;

namespace FiapGames.Shared.Kernel.Repositories;

public interface IRepository<TEntity> where TEntity : class, IEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<TEntity>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);

    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
