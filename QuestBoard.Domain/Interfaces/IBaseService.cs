namespace QuestBoard.Domain.Interfaces;

public interface IBaseService<T>
{
    /// <summary>
    /// Persists a new model and assigns its generated Id back onto the model.
    /// </summary>
    Task AddAsync(T model, CancellationToken token = default);

    /// <summary>
    /// Returns whether a record with the given Id exists.
    /// </summary>
    Task<bool> ExistsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns all records of this type.
    /// </summary>
    Task<IList<T>> GetAllAsync(CancellationToken token = default);

    /// <summary>
    /// Returns a single record by Id, or null if not found.
    /// </summary>
    Task<T?> GetByIdAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Deletes the given model.
    /// </summary>
    Task RemoveAsync(T model, CancellationToken token = default);

    /// <summary>
    /// Persists changes to an existing model.
    /// </summary>
    Task UpdateAsync(T model, CancellationToken token = default);
}