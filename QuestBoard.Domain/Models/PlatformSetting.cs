namespace QuestBoard.Domain.Models;

public class PlatformSetting : IModel
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int? GroupId { get; set; }
}
