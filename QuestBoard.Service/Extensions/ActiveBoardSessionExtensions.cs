using Microsoft.AspNetCore.Http;
using QuestBoard.Service.Constants;

namespace QuestBoard.Service.Extensions;

/// <summary>
/// Clears the active-board session keys. Setting them is the job of the active-board switcher;
/// clearing them lives here, so the two places that touch these keys stay few and named.
/// </summary>
public static class ActiveBoardSessionExtensions
{
    /// <summary>
    /// Removes the active board id, its name and its validation timestamp together so the three
    /// never drift apart. A session that still names a board that no longer exists would let a
    /// write stamp that board and fail on the foreign key.
    /// </summary>
    public static void ClearActiveBoard(this ISession session)
    {
        session.Remove(SessionKeys.ActiveGroupId);
        session.Remove(SessionKeys.ActiveGroupName);
        session.Remove(SessionKeys.ActiveGroupValidatedAtUtc);
    }

    /// <summary>
    /// Clears the active-board keys only when the session currently names <paramref name="groupId"/>.
    /// Returns true when it cleared them, false when the session names another board or none.
    /// </summary>
    public static async Task<bool> ClearActiveBoardIfAsync(this ISession session, int groupId, CancellationToken token = default)
    {
        await session.LoadAsync(token);

        if (session.GetInt32(SessionKeys.ActiveGroupId) != groupId)
        {
            return false;
        }

        session.ClearActiveBoard();
        return true;
    }
}
