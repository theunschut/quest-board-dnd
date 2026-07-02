using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("ShopItems")]
public class ShopItemEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int Type { get; set; }

    [Required]
    public int Rarity { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int Quantity { get; set; } = 0;

    [Required]
    public int Status { get; set; } = 0;

    [StringLength(500)]
    public string? ReferenceUrl { get; set; }

    [StringLength(1000)]
    public string? DenialReason { get; set; }

    public DateTime? DeniedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AvailableFrom { get; set; }

    public DateTime? AvailableUntil { get; set; }

    public int CreatedByDmId { get; set; }

    [ForeignKey(nameof(CreatedByDmId))]
    public virtual UserEntity CreatedByDm { get; set; } = null!;

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;

    public virtual ICollection<UserTransactionEntity> Transactions { get; set; } = [];
}