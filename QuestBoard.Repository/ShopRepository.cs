using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class ShopRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<ShopItem, ShopItemEntity>(dbContext, mapper), IShopRepository
{
    /// <inheritdoc/>
    public override async Task<IList<ShopItem>> GetAllAsync(CancellationToken token = default)
    {
        var entities = await DbContext.ShopItems
            .Include(si => si.CreatedByDm)
            .Include(si => si.Transactions)
            .OrderBy(si => si.Name)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<ShopItem>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetPublishedItemsAsync(CancellationToken token = default)
    {
        var entities = await DbContext.ShopItems
            .Include(si => si.CreatedByDm)
            .Where(si => si.Status == 1) // Published
            .Where(si => si.AvailableFrom == null || si.AvailableFrom <= DateTime.UtcNow)
            .Where(si => si.AvailableUntil == null || si.AvailableUntil >= DateTime.UtcNow)
            .OrderBy(si => si.Name)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<ShopItem>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetItemsByStatusAsync(int status, CancellationToken token = default)
    {
        var entities = await DbContext.ShopItems
            .Include(si => si.CreatedByDm)
            .Where(si => si.Status == status)
            .OrderByDescending(si => si.CreatedAt)
            .ThenBy(si => si.Name)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<ShopItem>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetItemsByTypeAsync(int type, CancellationToken token = default)
    {
        var entities = await DbContext.ShopItems
            .Include(si => si.CreatedByDm)
            .Where(si => si.Type == type && si.Status == 1) // Published
            .Where(si => si.AvailableFrom == null || si.AvailableFrom <= DateTime.UtcNow)
            .Where(si => si.AvailableUntil == null || si.AvailableUntil >= DateTime.UtcNow)
            .OrderBy(si => si.Name)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<ShopItem>>(entities);
    }

    /// <inheritdoc/>
    public async Task<ShopItem?> GetItemWithDetailsAsync(int id, CancellationToken token = default)
    {
        var entity = await DbContext.ShopItems
            .Include(si => si.CreatedByDm)
            .Include(si => si.Transactions)
                .ThenInclude(t => t.User)
            .FirstOrDefaultAsync(si => si.Id == id, cancellationToken: token);
        return entity == null ? null : Mapper.Map<ShopItem>(entity);
    }

    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetItemsByDmAsync(int dmId, CancellationToken token = default)
    {
        var entities = await DbContext.ShopItems
            .Include(si => si.CreatedByDm)
            .Where(si => si.CreatedByDmId == dmId)
            .OrderByDescending(si => si.CreatedAt)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<ShopItem>>(entities);
    }

    /// <inheritdoc/>
    public async Task<(IList<ShopItem> Items, int TotalCount)> GetPagedPublishedItemsAsync(
        int? type,
        IList<int>? rarityInts,
        string? sort,
        string? search,
        int page,
        int pageSize,
        CancellationToken token = default)
    {
        var query = DbContext.ShopItems
            .Include(si => si.CreatedByDm)
            .Where(si => si.Status == 1) // Published
            .Where(si => si.AvailableFrom == null || si.AvailableFrom <= DateTime.UtcNow)
            .Where(si => si.AvailableUntil == null || si.AvailableUntil >= DateTime.UtcNow);

        if (type.HasValue)
            query = query.Where(si => si.Type == type.Value);

        if (rarityInts is { Count: > 0 })
            query = query.Where(si => rarityInts.Contains(si.Rarity));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(si => si.Name.ToLower().Contains(searchLower) || si.Description.ToLower().Contains(searchLower));
        }

        var totalCount = await query.CountAsync(cancellationToken: token);

        query = sort switch
        {
            "price_asc" => query.OrderBy(si => (double)si.Price),
            "price_desc" => query.OrderByDescending(si => (double)si.Price),
            _ => query.OrderBy(si => si.Name)
        };

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken: token);

        return (Mapper.Map<IList<ShopItem>>(entities), totalCount);
    }
}
