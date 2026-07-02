using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.QuestViewModels;

public class FollowUpQuestViewModel
{
    /// <summary>Id of the original quest this is a follow-up to.</summary>
    [Required]
    public int OriginalQuestId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(1, 20, ErrorMessage = "Challenge Rating must be between 1 and 20.")]
    public int ChallengeRating { get; set; } = 1;

    [Required]
    public int DungeonMasterId { get; set; }

    [Required]
    [Range(1, 20, ErrorMessage = "Player count must be between 1 and 20.")]
    public int TotalPlayerCount { get; set; } = 6;

    /// <summary>Always false for new follow-up quests.</summary>
    public bool DungeonMasterSession { get; set; } = false;

    /// <summary>
    /// Must contain at least one date before saving.
    /// No default date — DM must add dates explicitly.
    /// Custom error message per UI-SPEC copywriting contract.
    /// </summary>
    public IList<DateTime> ProposedDates { get; set; } = [];
}
