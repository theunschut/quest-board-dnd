using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models.QuestBoard;

public class ProposedDate : IModel
{
    public int Id { get; set; }

    [Required]
    public int QuestId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    public Quest? Quest { get; set; }

    public IList<PlayerDateVote> PlayerVotes { get; set; } = [];
}