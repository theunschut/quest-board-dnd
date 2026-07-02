namespace QuestBoard.Domain.Models;

public class Group : IModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
