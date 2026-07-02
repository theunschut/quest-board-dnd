using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class UserGroup : IModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public GroupRole GroupRole { get; set; }
    public User? User { get; set; }
}
