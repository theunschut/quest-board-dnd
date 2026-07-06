using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("ContactImages")]
public class ContactImageEntity : IEntity
{
    [Key]
    [ForeignKey(nameof(Contact))]
    public int Id { get; set; }

    [Required]
    public byte[] ImageData { get; set; } = [];

    public virtual ContactEntity Contact { get; set; } = null!;
}
