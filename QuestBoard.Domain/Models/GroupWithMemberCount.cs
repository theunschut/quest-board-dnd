namespace QuestBoard.Domain.Models;

public class GroupWithMemberCount
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
}
