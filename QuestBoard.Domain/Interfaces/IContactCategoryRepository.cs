using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IContactCategoryRepository : IBaseRepository<ContactCategory>
{
    /// <summary>
    /// Returns every category in the active group ordered by sort position ascending, then by
    /// Id ascending so equal positions resolve by creation order. Returns an empty list when no
    /// board is active.
    /// </summary>
    Task<IList<ContactCategory>> GetOrderedForActiveGroupAsync(CancellationToken token = default);

    /// <summary>
    /// Returns a contact count per category Id, covering every category on the active board,
    /// including a zero entry for a category that holds no contacts.
    /// </summary>
    Task<IDictionary<int, int>> GetContactCountsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns zero for a board with no categories, or one more than the highest existing sort
    /// position otherwise.
    /// </summary>
    Task<int> GetNextSortOrderAsync(CancellationToken token = default);

    /// <summary>
    /// Exchanges the sort positions of the two given categories in a single save. Does nothing
    /// if either category is missing.
    /// </summary>
    Task SwapSortOrderAsync(int firstId, int secondId, CancellationToken token = default);

    /// <summary>
    /// Removes the category with the given Id and leaves every contact that referenced it
    /// present with a null category reference.
    /// </summary>
    Task DeleteWithDependentsLoadedAsync(int id, CancellationToken token = default);
}
