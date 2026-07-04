using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.QuestBoard;

public class PlayerSignup : IModel
{
    public int Id { get; set; }

    public required User Player { get; set; }

    public DateTime SignupTime { get; set; } = DateTime.UtcNow;

    // Ordering timestamp reset on every post-finalization vote change; null means the vote
    // has never changed since signup.
    public DateTime? LastVoteChangeTime { get; set; }

    public bool IsSelected { get; set; }

    public SignupRole Role { get; set; } = SignupRole.Player;

    public required Quest Quest { get; set; }

    public int? CharacterId { get; set; }

    public Character? Character { get; set; }

    public IList<PlayerDateVote> DateVotes { get; set; } = [];
}