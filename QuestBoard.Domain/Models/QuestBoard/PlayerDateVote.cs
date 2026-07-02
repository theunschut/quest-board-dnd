using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models.QuestBoard;

public class PlayerDateVote : IModel
{
    public int Id { get; set; }

    [Required]
    public int PlayerSignupId { get; set; }

    [Required]
    public int ProposedDateId { get; set; }

    public VoteType? Vote { get; set; }

    public PlayerSignup? PlayerSignup { get; set; }
    public ProposedDate? ProposedDate { get; set; }
}