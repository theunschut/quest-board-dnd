namespace QuestBoard.Domain.Models;

public class DungeonMasterProfile : IModel
{
    public int Id { get; set; }           // = UserId
    public string? Bio { get; set; }
    public byte[]? ProfilePicture { get; set; }
}
