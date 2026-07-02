namespace QuestBoard.Repository.Entities;

/// <summary>Marker interface implemented by every EF Core entity to guarantee an identity property.</summary>
public interface IEntity
{
    /// <summary>The entity's primary key.</summary>
    public int Id { get; }
}