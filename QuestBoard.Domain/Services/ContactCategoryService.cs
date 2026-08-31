using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class ContactCategoryService(IContactCategoryRepository repository, IMapper mapper) : BaseService<ContactCategory>(repository, mapper), IContactCategoryService
{
    /// <inheritdoc/>
    public async Task<IList<ContactCategory>> GetOrderedAsync(CancellationToken token = default)
    {
        return await repository.GetOrderedForActiveGroupAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IDictionary<int, int>> GetContactCountsAsync(CancellationToken token = default)
    {
        return await repository.GetContactCountsAsync(token);
    }

    /// <inheritdoc/>
    public async Task AddToEndAsync(ContactCategory category, CancellationToken token = default)
    {
        // The board Id is stamped by the controller from the active board before this is
        // called, exactly as ContactsController.Create stamps it onto a new contact -- this
        // service does not read board state itself.
        category.SortOrder = await repository.GetNextSortOrderAsync(token);
        await repository.AddAsync(category, token);
    }

    /// <inheritdoc/>
    public async Task<bool> MoveUpAsync(int id, CancellationToken token = default)
    {
        var ordered = await repository.GetOrderedForActiveGroupAsync(token);
        var index = FindIndex(ordered, id);
        // Absent, or already first -- nothing to swap with.
        if (index < 0 || index == 0) return false;

        await repository.SwapSortOrderAsync(ordered[index].Id, ordered[index - 1].Id, token);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> MoveDownAsync(int id, CancellationToken token = default)
    {
        var ordered = await repository.GetOrderedForActiveGroupAsync(token);
        var index = FindIndex(ordered, id);
        // Absent, or already last -- nothing to swap with.
        if (index < 0 || index == ordered.Count - 1) return false;

        await repository.SwapSortOrderAsync(ordered[index].Id, ordered[index + 1].Id, token);
        return true;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id, CancellationToken token = default)
    {
        await repository.DeleteWithDependentsLoadedAsync(id, token);
    }

    // Neighbours are computed from position in the ordered list rather than from arithmetic on
    // the sort value -- positions are dense today but a tie or a gap must not produce a wrong
    // neighbour.
    private static int FindIndex(IList<ContactCategory> ordered, int id)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Id == id) return i;
        }
        return -1;
    }
}
