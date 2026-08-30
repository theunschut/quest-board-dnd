using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class ContactCategoryRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<ContactCategory, ContactCategoryEntity>(dbContext, mapper), IContactCategoryRepository
{
    /// <inheritdoc/>
    public async Task<IList<ContactCategory>> GetOrderedForActiveGroupAsync(CancellationToken token = default)
    {
        // Group scoping is enforced entirely by ContactCategoryEntity's fail-closed query filter
        // here -- no manual GroupId .Where is needed or added. The secondary key on Id is the
        // tie-break the entity comment documents; every ordered read in this phase uses the same
        // two keys so the index and the management page can never disagree.
        var entities = await DbContext.ContactCategories
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(token);

        return Mapper.Map<IList<ContactCategory>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IDictionary<int, int>> GetContactCountsAsync(CancellationToken token = default)
    {
        // Both DbSets are already board-scoped by their own filters, so the counts cannot
        // include another board's contacts.
        var categoryIds = await DbContext.ContactCategories
            .Select(c => c.Id)
            .ToListAsync(token);

        var counts = await DbContext.Contacts
            .Where(c => c.CategoryId != null)
            .GroupBy(c => c.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, token);

        // A category with no contacts is missing from the grouped result entirely -- carry a
        // zero entry for it so every category on the board has an entry.
        return categoryIds.ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }

    /// <inheritdoc/>
    public async Task<int> GetNextSortOrderAsync(CancellationToken token = default)
    {
        var hasAny = await DbContext.ContactCategories.AnyAsync(token);
        if (!hasAny) return 0;

        return await DbContext.ContactCategories.MaxAsync(c => c.SortOrder, token) + 1;
    }

    /// <inheritdoc/>
    public async Task SwapSortOrderAsync(int firstId, int secondId, CancellationToken token = default)
    {
        var first = await DbContext.ContactCategories.FirstOrDefaultAsync(c => c.Id == firstId, token);
        var second = await DbContext.ContactCategories.FirstOrDefaultAsync(c => c.Id == secondId, token);
        // A stale button (either category deleted or moved out of the active board between the
        // page render and the click) is harmless -- no-op rather than a partial swap.
        if (first == null || second == null) return;

        (first.SortOrder, second.SortOrder) = (second.SortOrder, first.SortOrder);

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task DeleteWithDependentsLoadedAsync(int id, CancellationToken token = default)
    {
        var entity = await DbContext.ContactCategories.FirstOrDefaultAsync(c => c.Id == id, token);
        if (entity == null) return;

        // Loading the dependent contacts into the change tracker (rather than nulling out each
        // one's category reference by hand) is what makes the in-memory test provider apply the
        // same configured SetNull delete behaviour that SQL Server applies, so this path behaves
        // identically under test and in production.
        await DbContext.Contacts
            .Where(c => c.CategoryId == id)
            .ToListAsync(token);

        DbContext.ContactCategories.Remove(entity);

        await DbContext.SaveChangesAsync(token);
    }
}
