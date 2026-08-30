using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IContactCategoryService : IBaseService<ContactCategory>
{
    /// <summary>
    /// Returns every category in the active group ordered by sort position ascending, then by
    /// Id ascending so equal positions resolve by creation order.
    /// </summary>
    Task<IList<ContactCategory>> GetOrderedAsync(CancellationToken token = default);

    /// <summary>
    /// Returns a contact count per category Id, covering every category on the active board,
    /// including a zero entry for a category that holds no contacts.
    /// </summary>
    Task<IDictionary<int, int>> GetContactCountsAsync(CancellationToken token = default);

    /// <summary>
    /// Stamps the next sort position onto the category and persists it, so it lands after every
    /// existing category on its board.
    /// </summary>
    Task AddToEndAsync(ContactCategory category, CancellationToken token = default);

    /// <summary>
    /// Swaps the given category with its immediate predecessor in sort order. Returns false and
    /// writes nothing if the category is already first (or absent).
    /// </summary>
    Task<bool> MoveUpAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Swaps the given category with its immediate successor in sort order. Returns false and
    /// writes nothing if the category is already last (or absent).
    /// </summary>
    Task<bool> MoveDownAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Removes the category, leaving its contacts present with no category reference.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken token = default);
}
