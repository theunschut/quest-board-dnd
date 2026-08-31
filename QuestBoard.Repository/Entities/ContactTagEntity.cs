using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("ContactTags")]
public class ContactTagEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    // Capped short so a tag renders as an inline chip rather than a section heading — a long
    // name wraps badly on the mobile stacked-row layout.
    [Required]
    [StringLength(30)]
    public string Name { get; set; } = string.Empty;

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;

    public virtual ICollection<ContactEntity> Contacts { get; set; } = [];
}
