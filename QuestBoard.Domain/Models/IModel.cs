namespace QuestBoard.Domain.Models;

/// <summary>Marker interface implemented by every Domain model to guarantee an identity property.</summary>
public interface IModel
{
    /// <summary>The model's primary identifier.</summary>
    public int Id { get; set; }
}