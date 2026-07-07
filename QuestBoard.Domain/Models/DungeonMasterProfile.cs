namespace QuestBoard.Domain.Models;

public class DungeonMasterProfile : IModel
{
    public int Id { get; set; }           // = UserId
    public string? Bio { get; set; }
    public byte[]? ProfilePicture { get; set; }

    // Lets list/detail views show a placeholder-or-photo state without pulling the image bytes.
    public bool HasProfilePicture { get; set; }
}
