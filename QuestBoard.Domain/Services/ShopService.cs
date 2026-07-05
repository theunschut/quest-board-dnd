using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.Domain.Services;

internal class ShopService(IShopRepository repository, IUserTransactionRepository transactionRepository, IMapper mapper) : BaseService<ShopItem>(repository, mapper), IShopService
{
    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetAllItemsAsync(CancellationToken token = default)
    {
        return await repository.GetAllAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetPublishedItemsAsync(CancellationToken token = default)
    {
        return await repository.GetPublishedItemsAsync(token);
    }

    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetItemsByStatusAsync(ItemStatus status, CancellationToken token = default)
    {
        return await repository.GetItemsByStatusAsync((int)status, token);
    }

    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetItemsByTypeAsync(ItemType type, CancellationToken token = default)
    {
        return await repository.GetItemsByTypeAsync((int)type, token);
    }

    /// <inheritdoc/>
    public async Task<ShopItem?> GetItemWithDetailsAsync(int id, CancellationToken token = default)
    {
        return await repository.GetItemWithDetailsAsync(id, token);
    }

    /// <inheritdoc/>
    public async Task<IList<ShopItem>> GetItemsByDmAsync(int dmId, CancellationToken token = default)
    {
        return await repository.GetItemsByDmAsync(dmId, token);
    }

    // Business logic methods

    /// <inheritdoc/>
    public Task<decimal> CalculateItemPriceAsync(ItemRarity rarity, CancellationToken token = default)
    {
        // Implement Tasha's Cauldron pricing guidelines
        return Task.FromResult(rarity switch
        {
            ItemRarity.Common => 100m,
            ItemRarity.Uncommon => 500m,
            ItemRarity.Rare => 5000m,
            ItemRarity.VeryRare => 50000m,
            ItemRarity.Legendary => 200000m,
            _ => 100m
        });
    }

    /// <inheritdoc/>
    public async Task PublishItemAsync(int itemId, CancellationToken token = default)
    {
        var item = await repository.GetByIdAsync(itemId, token);
        if (item != null)
        {
            item.Status = ItemStatus.Published;
            await repository.UpdateAsync(item, token);
        }
    }

    /// <inheritdoc/>
    public async Task<UserTransaction> PurchaseItemAsync(int itemId, int quantity, User user, CancellationToken token = default)
    {
        var item = await repository.GetByIdAsync(itemId, token);
        if (item == null || item.Status != ItemStatus.Published)
        {
            throw new InvalidOperationException("Item is not available for purchase.");
        }

        // Check stock availability (-1 = unlimited, 0 = sold out, >0 = limited stock)
        if (item.Quantity == 0)
        {
            throw new InvalidOperationException("This item is sold out.");
        }

        // Only check quantity limits if not unlimited stock
        if (item.Quantity > 0 && item.Quantity < quantity)
        {
            throw new InvalidOperationException($"Only {item.Quantity} items available in stock.");
        }

        // Update item quantity only if it's limited stock (quantity > 0)
        if (item.Quantity > 0)
        {
            item.Quantity -= quantity;
            await repository.UpdateAsync(item, token);
        }

        // Create transaction record
        var transaction = new UserTransaction
        {
            ShopItemId = itemId,
            UserId = user.Id,
            Quantity = quantity,
            Price = item.Price * quantity,
            TransactionType = TransactionType.Purchase,
            TransactionDate = DateTime.UtcNow,
            Notes = $"Purchase of {quantity}x {item.Name}"
        };

        await transactionRepository.AddAsync(transaction, token);
        return transaction;
    }

    /// <inheritdoc/>
    public async Task<UserTransaction> ReturnOrSellItemAsync(int transactionId, int quantity, User user, CancellationToken token = default)
    {
        var originalTransaction = await transactionRepository.GetTransactionWithDetailsAsync(transactionId, token);
        if (originalTransaction == null || originalTransaction.UserId != user.Id || originalTransaction.TransactionType != TransactionType.Purchase)
        {
            throw new InvalidOperationException("Original purchase transaction not found or does not belong to the user.");
        }

        // Check how much has already been returned/sold for this original purchase
        var allUserTransactions = await transactionRepository.GetTransactionsByUserAsync(user.Id, token);
        var remainingQuantity = CalculateRemainingQuantity(originalTransaction, allUserTransactions);

        if (remainingQuantity <= 0)
        {
            throw new InvalidOperationException("This item has already been fully returned/sold.");
        }

        if (quantity > remainingQuantity)
        {
            throw new InvalidOperationException($"Cannot return/sell more items than remaining. Only {remainingQuantity} items can still be returned/sold.");
        }

        var item = await repository.GetByIdAsync(originalTransaction.ShopItemId, token)
            ?? throw new InvalidOperationException("Original item no longer exists.");

        // Calculate time since purchase
        var timeSincePurchase = DateTime.UtcNow - originalTransaction.TransactionDate;
        var isReturn = timeSincePurchase.TotalHours <= 24;

        // Calculate refund amount
        var originalUnitPrice = originalTransaction.Price / originalTransaction.Quantity;
        var refundAmount = isReturn ? originalUnitPrice * quantity : (originalUnitPrice * quantity * 0.5m);

        // Update item quantity if it's not unlimited (quantity != -1)
        if (item.Quantity >= 0)
        {
            item.Quantity += quantity;
            await repository.UpdateAsync(item, token);
        }

        // Create refund transaction record
        var refundTransaction = new UserTransaction
        {
            ShopItemId = originalTransaction.ShopItemId,
            UserId = user.Id,
            Quantity = quantity,
            Price = refundAmount,
            TransactionType = TransactionType.Sell,
            TransactionDate = DateTime.UtcNow,
            OriginalTransactionId = transactionId,
            Notes = $"{(isReturn ? "Return" : "Sell")} of {quantity}x {item.Name}"
        };

        await transactionRepository.AddAsync(refundTransaction, token);
        return refundTransaction;
    }

    /// <inheritdoc/>
    public async Task<UserTransaction> SellItemToShopAsync(int itemId, int quantity, User user, CancellationToken token = default)
    {
        var item = await repository.GetByIdAsync(itemId, token);
        if (item == null || item.Status != ItemStatus.Published)
        {
            throw new InvalidOperationException("Item is not available for sale.");
        }

        // Calculate sell price (half of shop price)
        var sellPrice = (item.Price / 2) * quantity;

        // Update item quantity if it's not unlimited (quantity != -1)
        if (item.Quantity >= 0)
        {
            item.Quantity += quantity;
            await repository.UpdateAsync(item, token);
        }

        // Create sell transaction record
        var sellTransaction = new UserTransaction
        {
            ShopItemId = itemId,
            UserId = user.Id,
            Quantity = quantity,
            Price = sellPrice,
            TransactionType = TransactionType.Sell,
            TransactionDate = DateTime.UtcNow,
            OriginalTransactionId = null,
            Notes = $"Sold {quantity}x {item.Name} to shop"
        };

        await transactionRepository.AddAsync(sellTransaction, token);
        return sellTransaction;
    }

    /// <inheritdoc/>
    public async Task ArchiveItemAsync(int itemId, CancellationToken token = default)
    {
        var item = await repository.GetByIdAsync(itemId, token);
        if (item != null)
        {
            item.Status = ItemStatus.Archived;
            await repository.UpdateAsync(item, token);
        }
    }

    /// <inheritdoc/>
    public async Task DenyItemAsync(int itemId, string denialReason, CancellationToken token = default)
    {
        var item = await repository.GetByIdAsync(itemId, token);
        if (item != null)
        {
            item.Status = ItemStatus.Denied;
            item.DenialReason = denialReason;
            item.DeniedAt = DateTime.UtcNow;
            await repository.UpdateAsync(item, token);
        }
    }

    /// <inheritdoc/>
    public async Task<IList<UserTransaction>> GetUserTransactionsAsync(int userId, CancellationToken token = default)
    {
        return await transactionRepository.GetTransactionsByUserAsync(userId, token);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TransactionWithRemaining>> GetUserTransactionsWithRemainingAsync(int userId, CancellationToken token = default)
    {
        var all = await transactionRepository.GetTransactionsByUserAsync(userId, token);
        return all
            .Where(t => t.TransactionType == TransactionType.Purchase)
            .Select(t => new TransactionWithRemaining(t, CalculateRemainingQuantity(t, all)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IList<UserTransaction>> GetAllTransactionsAsync(CancellationToken token = default)
    {
        return await transactionRepository.GetAllAsync(token);
    }

    /// <inheritdoc/>
    public async Task<(IList<ShopItem> Items, int TotalCount)> GetPagedPublishedItemsAsync(
        ItemType? type,
        IList<ItemRarity>? rarities,
        string? sort,
        string? search,
        int page,
        int pageSize,
        CancellationToken token = default)
    {
        var rarityInts = rarities?.Select(r => (int)r).ToList();
        return await repository.GetPagedPublishedItemsAsync(
            type.HasValue ? (int?)type.Value : null,
            rarityInts,
            sort,
            search,
            page,
            pageSize,
            token);
    }

    private static int CalculateRemainingQuantity(UserTransaction purchase, IList<UserTransaction> allTransactions)
    {
        var returned = allTransactions
            .Where(t => t.TransactionType == TransactionType.Sell &&
                        t.OriginalTransactionId == purchase.Id)
            .Sum(t => t.Quantity);
        return purchase.Quantity - returned;
    }
}
