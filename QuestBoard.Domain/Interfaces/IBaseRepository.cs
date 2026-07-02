namespace QuestBoard.Domain.Interfaces;

public interface IBaseRepository<T>
{
    /// <summary>
    /// Maps the model to its entity, persists it, and propagates the DB-generated Id back onto the model.
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
    /// Deletes the entity matching the given model's Id, if it exists.
    /// </summary>
    Task RemoveAsync(T model, CancellationToken token = default);

    /// <summary>
    /// Persists any pending changes tracked on the underlying DbContext.
    /// </summary>
    Task SaveChangesAsync(CancellationToken token = default);

    /// <summary>
    /// Maps the model's changes onto the tracked entity matching its Id and persists them.
    /// </summary>
    Task UpdateAsync(T model, CancellationToken token = default);
}
