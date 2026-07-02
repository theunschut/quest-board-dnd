using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.QuestBoard;

public class PlayerSignup : IModel
{
    public int Id { get; set; }

    public required User Player { get; set; }

    public DateTime SignupTime { get; set; } = DateTime.UtcNow;

    public bool IsSelected { get; set; }

    public SignupRole Role { get; set; } = SignupRole.Player;

    public required Quest Quest { get; set; }

    public int? CharacterId { get; set; }

    public Character? Character { get; set; }

    public IList<PlayerDateVote> DateVotes { get; set; } = [];
}