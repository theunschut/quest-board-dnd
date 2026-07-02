namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class ShopCategoryViewModel
{
    public string Title { get; set; } = string.Empty;
    public IList<ShopItemViewModel> Items { get; set; } = [];
}