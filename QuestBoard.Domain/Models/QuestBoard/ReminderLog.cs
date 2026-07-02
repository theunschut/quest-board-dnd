namespace QuestBoard.Domain.Models.QuestBoard;

public class ReminderLog : IModel
{
    public int Id { get; set; }
    public int QuestId { get; set; }
    public int PlayerId { get; set; }
    public DateTime SentAt { get; set; }
}
