using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("PlayerDateVotes")]
public class PlayerDateVoteEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int PlayerSignupId { get; set; }

    [Required]
    public int ProposedDateId { get; set; }

    public int? Vote { get; set; }

    [ForeignKey(nameof(PlayerSignupId))]
    public virtual PlayerSignupEntity PlayerSignup { get; set; } = null!;

    [ForeignKey(nameof(ProposedDateId))]
    public virtual ProposedDateEntity ProposedDate { get; set; } = null!;
}