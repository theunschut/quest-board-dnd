using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Services;

/// <summary>
/// Reads the active board's page settings once per request. The board type comes from the existing
/// <see cref="IBoardTypeResolver"/>, so there is no second board-type lookup. Nothing is cached in
/// the session or the claims: a settings change applies on the very next request.
/// </summary>
/// <remarks>
/// The answer is remembered per scope but keyed on the active board id rather than on the request,
/// because the board picker can switch boards partway through one request.
/// </remarks>
public sealed class BoardPageResolver(
    IActiveGroupContext activeGroupContext,
    IBoardTypeResolver boardTypeResolver,
    IGroupService groupService) : IBoardPageResolver
{
    private bool _hasMemo;
    private int? _memoGroupId;
    private BoardPageAvailability? _memo;

    public async Task<BoardPageAvailability?> GetAsync(CancellationToken token = default)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (_hasMemo && _memoGroupId == groupId)
        {
            return _memo;
        }

        var type = await boardTypeResolver.GetBoardTypeAsync(token);
        BoardPageAvailability? availability = null;

        if (type is { } boardType)
        {
            // The board-type resolver has just loaded this row, so this read costs no second query.
            // A missing row means nothing is switched off and the quest list is the home.
            var group = groupId is { } id ? await groupService.GetByIdAsync(id, token) : null;
            availability = new BoardPageAvailability(
                boardType,
                group?.DisabledPages ?? BoardPageSet.Empty,
                group?.HomePage ?? BoardPage.Quests);
        }

        _hasMemo = true;
        _memoGroupId = groupId;
        _memo = availability;
        return availability;
    }
}
