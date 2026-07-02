using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models.Shop;

public class TradeItem : IModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int OfferedByPlayerId { get; set; }

    public User? OfferedByPlayer { get; set; }

    [StringLength(200)]
    public string? WantedItem { get; set; }

    [Required]
    public TradeStatus Status { get; set; } = TradeStatus.Available;

    public DateTime ListedDate { get; set; } = DateTime.UtcNow;

    public decimal? SuggestedPrice { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public enum TradeStatus
{
    Available,
    Pending,
    Completed,
    Cancelled
}