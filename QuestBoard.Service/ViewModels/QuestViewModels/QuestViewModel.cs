using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.QuestViewModels;

public class QuestViewModel
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? Rewards { get; set; }

    [Required]
    [Range(1, 20, ErrorMessage = "Challenge Rating must be between 1 and 20.")]
    public int ChallengeRating { get; set; } = 1;

    [Required]
    public int DungeonMasterId { get; set; }

    public int TotalPlayerCount { get; set; } = 6;

    public bool DungeonMasterSession { get; set; }

    public IList<DateTime> ProposedDates { get; set; } = [];
}