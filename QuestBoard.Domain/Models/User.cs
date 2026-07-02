using QuestBoard.Domain.Models.QuestBoard;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models;

public class User : IModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; }

    public bool HasKey { get; set; }

    public bool EmailConfirmed { get; set; }

    public IList<Quest> Quests { get; set; } = [];

    public IList<PlayerSignup> Signups { get; set; } = [];

    public override bool Equals(object? obj)
    {
        return obj is User user&&
               Id==user.Id&&
               Name==user.Name&&
               Email==user.Email&&
               HasKey==user.HasKey&&
               EmailConfirmed==user.EmailConfirmed;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Email, HasKey, EmailConfirmed);
    }
}