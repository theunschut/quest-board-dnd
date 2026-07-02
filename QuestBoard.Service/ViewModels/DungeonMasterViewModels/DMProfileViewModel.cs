namespace QuestBoard.Service.ViewModels.DungeonMasterViewModels;

public class DMProfileViewModel
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public bool HasProfilePicture { get; set; }
    public bool CanEdit { get; set; }
    public List<QuestSummaryViewModel> Quests { get; set; } = [];
}

public class QuestSummaryViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public int ChallengeRating { get; set; }
}
