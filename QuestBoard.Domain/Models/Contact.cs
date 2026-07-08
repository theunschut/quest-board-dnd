using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models;

public class Contact : IModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public byte[]? ContactImageData { get; set; }

    // Lets list/detail views show a placeholder-or-photo state without pulling the image bytes.
    public bool HasContactImage { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(200)]
    public string? TownCity { get; set; }

    [StringLength(200)]
    public string? SubLocation { get; set; }

    public bool IsRevealed { get; set; } = false;

    public int CreatedByUserId { get; set; }

    public User CreatedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GroupId { get; set; }

    // Freeform, author-attributed, timestamped notes any group member can add to and edit.
    public IList<ContactNote> Notes { get; set; } = [];
}

public class ContactNote : IModel
{
    public int Id { get; set; }

    public int ContactId { get; set; }

    [StringLength(2000)]
    public string Text { get; set; } = string.Empty;

    public int AuthorUserId { get; set; }

    // Display-only; populated from the note's Author navigation via mapping.
    public string? AuthorName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
