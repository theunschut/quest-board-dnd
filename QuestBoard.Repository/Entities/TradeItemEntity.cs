using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("TradeItems")]
public class TradeItemEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int OfferedByPlayerId { get; set; }

    [ForeignKey(nameof(OfferedByPlayerId))]
    public virtual UserEntity OfferedByPlayer { get; set; } = null!;

    [StringLength(200)]
    public string? WantedItem { get; set; }

    [Required]
    public int Status { get; set; } = 0;

    public DateTime ListedDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SuggestedPrice { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}