using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models;

public class ContactCategory : IModel
{
    public int Id { get; set; }

    // Capped shorter than Contact.Name (100) because this value renders as a heading that has
    // to survive a narrow phone screen.
    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    // Positions are dense within a board: a new category takes the highest existing position
    // plus one, or zero when the board has none. Every ordered read is by SortOrder then by Id,
    // so equal positions resolve by creation order. Reordering swaps two positions rather than
    // renumbering the whole list.
    public int SortOrder { get; set; }

    public int GroupId { get; set; }
}
