using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal abstract class BaseService<TModel>(IBaseRepository<TModel> repository, IMapper mapper) : IBaseService<TModel>
    where TModel : class, IModel
{
    protected IMapper Mapper => mapper;

    /// <inheritdoc/>
    public virtual async Task AddAsync(TModel model, CancellationToken token = default)
    {
        await repository.AddAsync(model, token);
    }

    /// <inheritdoc/>
    public virtual async Task<bool> ExistsAsync(int id, CancellationToken token = default)
    {
        return await repository.ExistsAsync(id, token);
    }

    /// <inheritdoc/>
    public virtual async Task<IList<TModel>> GetAllAsync(CancellationToken token = default)
    {
        return await repository.GetAllAsync(token);
    }

    /// <inheritdoc/>
    public virtual async Task<TModel?> GetByIdAsync(int id, CancellationToken token = default)
    {
        return await repository.GetByIdAsync(id, token);
    }

    /// <inheritdoc/>
    public virtual async Task RemoveAsync(TModel model, CancellationToken token = default)
    {
        await repository.RemoveAsync(model, token);
    }

    public Task SaveChangesAsync(CancellationToken token = default) => repository.SaveChangesAsync(token);

    /// <inheritdoc/>
    public virtual async Task UpdateAsync(TModel model, CancellationToken token = default)
    {
        await repository.UpdateAsync(model, token);
    }
}
