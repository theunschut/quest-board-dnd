using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Quests")]
public class QuestEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int ChallengeRating { get; set; } = 1;

    public int DungeonMasterId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FinalizedDate { get; set; }

    public bool IsFinalized { get; set; }

    public DateTime? FinalizedEmailSentForDate { get; set; }

    public DateTime? ClosedDate { get; set; }

    public bool IsClosed { get; set; }

    public int TotalPlayerCount { get; set; }

    public bool DungeonMasterSession { get; set; }

    public string? Recap { get; set; }

    public string? Rewards { get; set; }

    public int? OriginalQuestId { get; set; }

    [ForeignKey(nameof(OriginalQuestId))]
    public virtual QuestEntity? OriginalQuest { get; set; }

    public virtual QuestEntity? FollowUpQuest { get; set; }

    [ForeignKey(nameof(DungeonMasterId))]
    public virtual UserEntity DungeonMaster { get; set; } = null!;

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;

    public virtual ICollection<ProposedDateEntity> ProposedDates { get; set; } = [];

    public virtual ICollection<PlayerSignupEntity> PlayerSignups { get; set; } = [];
}