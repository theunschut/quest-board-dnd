using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class ShopItemDetailsViewModel : ShopItemViewModel
{
    public IList<DmItemVoteViewModel> DmVotes { get; set; } = [];
    public IList<PlayerTransactionViewModel> RecentTransactions { get; set; } = [];

    public int YesVotes => DmVotes.Count(v => v.VoteType == VoteType.Yes);
    public int NoVotes => DmVotes.Count(v => v.VoteType == VoteType.No);
    public int MaybeVotes => DmVotes.Count(v => v.VoteType == VoteType.Maybe);
}

public class DmItemVoteViewModel
{
    public string DmName { get; set; } = string.Empty;
    public VoteType VoteType { get; set; }
    public DateTime VoteDate { get; set; }
}

public class PlayerTransactionViewModel
{
    public string PlayerName { get; set; } = string.Empty;
    public TransactionType TransactionType { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public DateTime TransactionDate { get; set; }
}