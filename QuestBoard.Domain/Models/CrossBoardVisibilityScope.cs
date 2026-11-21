namespace QuestBoard.Domain.Models;

/// <summary>
/// What the cross-board lookup needs to know about the viewer and the board's clock to apply the
/// same visibility rule the target page applies to a record. It holds ids and times only, never
/// board content, and every board named in it is one the viewer already belongs to.
/// </summary>
/// <param name="ViewerId">The viewer, for rules that turn on who owns or created a record.</param>
/// <param name="CandidateGroupIds">The viewer's own boards the link may still take them to.</param>
/// <param name="DungeonMasterTierGroupIds">
/// The candidate boards where the viewer is a Dungeon Master or Admin, or a SuperAdmin member. This
/// is the one set that means "may review everything on this board" for every record rule.
/// </param>
/// <param name="AdminGroupIds">The candidate boards where the viewer is an Admin, or a SuperAdmin member.</param>
/// <param name="BoardNow">The board's own local time, for sale windows.</param>
/// <param name="BoardTodayStart">The start of the board's own today, for the played-quest rule.</param>
public sealed record CrossBoardVisibilityScope(
    int ViewerId,
    IReadOnlyCollection<int> CandidateGroupIds,
    IReadOnlyCollection<int> DungeonMasterTierGroupIds,
    IReadOnlyCollection<int> AdminGroupIds,
    DateTime BoardNow,
    DateTime BoardTodayStart);
