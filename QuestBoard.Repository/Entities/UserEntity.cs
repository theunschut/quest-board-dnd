using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Repository.Entities;

public class UserEntity : IdentityUser<int>, IEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool HasKey { get; set; }

    public virtual ICollection<QuestEntity> Quests { get; set; } = [];

    public ICollection<PlayerSignupEntity> Signups { get; set; } = [];

    public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];
}