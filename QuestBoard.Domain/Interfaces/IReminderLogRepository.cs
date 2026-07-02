namespace QuestBoard.Domain.Interfaces;

public interface IReminderLogRepository
{
    /// <summary>
    /// Returns whether a session-reminder email has already been logged for the given quest/player pair.
    /// </summary>
    Task<bool> ExistsAsync(int questId, int playerId, CancellationToken token = default);

    /// <summary>
    /// Records that a session-reminder email was sent for the given quest/player pair.
    /// Safe under concurrent calls — a duplicate insert from a racing job is caught and ignored.
    /// </summary>
    Task AddAsync(int questId, int playerId, CancellationToken token = default);
}
