namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class ShopManagementIndexViewModel
{
    public IList<ShopItemViewModel> MyItems { get; set; } = [];
    public IList<ShopItemViewModel> ItemsForReview { get; set; } = [];
    public IList<ShopItemViewModel> AllOtherItems { get; set; } = [];
}