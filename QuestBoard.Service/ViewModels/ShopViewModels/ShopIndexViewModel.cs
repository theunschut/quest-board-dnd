using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class ShopIndexViewModel
{
    public IList<ShopItemViewModel> Items { get; set; } = [];
    public ItemType? SelectedType { get; set; }
    public IList<ItemRarity> SelectedRarities { get; set; } = [];
    public string? SelectedSort { get; set; }
    public string? SearchQuery { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; } = 0;
    public bool HasActiveSearch => !string.IsNullOrEmpty(SearchQuery);
    public bool HasActiveFilters => SelectedRarities.Count > 0 || SelectedSort != null || HasActiveSearch;
    public IList<UserTransactionViewModel> UserPurchases { get; set; } = [];
}
