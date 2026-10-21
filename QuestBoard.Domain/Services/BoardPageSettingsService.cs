using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class BoardPageSettingsService(IEventRepository eventRepository, IQuestRepository questRepository, IBoardClock boardClock) : IBoardPageSettingsService
{
    /// <inheritdoc/>
    public async Task<BoardPageSettingsOutcome> EvaluateAsync(Group group, BoardPageSettingsRequest request, CancellationToken token = default)
    {
        var errors = new List<string>();

        var disabled = ApplySwitches(group, request);
        var home = EvaluateHome(group, request, disabled, errors);

        // The counts below are read first and the settings are saved afterwards, with nothing
        // holding the board's content still in between. A quest or event created in that gap is
        // not counted and stays behind the page that was just switched off. The gap is a few
        // milliseconds on a super-admin-only save, and the content is only hidden, never deleted,
        // so switching the page back on brings it straight back; that is why this does not take a
        // serializable transaction across the count and the save.
        //
        // Switching Quests off strands every quest that is still open or upcoming behind a page
        // nobody can open, so the live ones are counted, but only on the transition from on to off.
        if (!group.DisabledPages.Contains(BoardPage.Quests) && disabled.Contains(BoardPage.Quests))
        {
            await EvaluateQuestsOffAsync(group.Id, errors, token);
        }

        // Switching Events off strands anything dated today or later behind a page nobody can
        // open, and a running series would keep creating more, so both are checked, but only on
        // the transition from on to off.
        if (!group.DisabledPages.Contains(BoardPage.Events) && disabled.Contains(BoardPage.Events))
        {
            await EvaluateEventsOffAsync(group.Id, errors, token);
        }

        return new BoardPageSettingsOutcome(disabled, home, errors);
    }

    // The type ceiling is enforced here and not only in the form: a switch the board's type does
    // not allow, a page that cannot be switched, an undefined page value and a page missing from
    // the request all leave the stored bit alone.
    private static BoardPageSet ApplySwitches(Group group, BoardPageSettingsRequest request)
    {
        var result = group.DisabledPages;

        foreach (var (page, on) in request.Switches)
        {
            if (page == BoardPage.QuestLog || !CanChange(group.BoardType, page))
            {
                continue;
            }

            result = on ? result.Without(page) : result.With(page);
        }

        // The Quest Log needs the quest list, so its switch is read against the state the other
        // switches just produced.
        if (request.Switches.TryGetValue(BoardPage.QuestLog, out var questLogOn)
            && BoardPagePolicy.IsAvailable(group.BoardType, result, BoardPage.Quests))
        {
            result = questLogOn ? result.Without(BoardPage.QuestLog) : result.With(BoardPage.QuestLog);
        }

        return result;
    }

    private static bool CanChange(BoardType type, BoardPage page) =>
        Enum.IsDefined(page)
        && BoardPagePolicy.IsSwitchable(page)
        && BoardPagePolicy.AllowedOn(type, page);

    private static BoardPage EvaluateHome(Group group, BoardPageSettingsRequest request, BoardPageSet disabled, List<string> errors)
    {
        var home = group.HomePage;

        if (request.HomePage is { } requested && requested != group.HomePage)
        {
            if (!Enum.IsDefined(requested) || !BoardPagePolicy.IsHomeCandidate(requested))
            {
                errors.Add("Choose a home page from the list.");
                return home;
            }

            home = requested;
            if (!BoardPagePolicy.IsAvailable(group.BoardType, disabled, requested))
            {
                errors.Add($"{BoardPagePolicy.Label(requested)} can't be the home page because it isn't available on this board.");
            }

            return home;
        }

        // The home is unchanged. Refuse only when this request is what takes it away: a stored
        // home that was already unavailable falls back on its own, so an unrelated save on such a
        // board must still succeed.
        var wasAvailable = BoardPagePolicy.IsAvailable(group.BoardType, group.DisabledPages, home);
        if (wasAvailable && !BoardPagePolicy.IsAvailable(group.BoardType, disabled, home))
        {
            errors.Add($"{BoardPagePolicy.Label(home)} is this board's home page; pick another home page first.");
        }

        return home;
    }

    private async Task EvaluateQuestsOffAsync(int groupId, List<string> errors, CancellationToken token)
    {
        var live = await questRepository.CountLiveForBoardAsync(groupId, boardClock.Today, token);
        if (live > 0)
        {
            var noun = live == 1 ? "1 live quest" : $"{live} live quests";
            errors.Add($"Quests can't be switched off while this board has {noun}. Close or delete them first.");
        }
    }

    private async Task EvaluateEventsOffAsync(int groupId, List<string> errors, CancellationToken token)
    {
        var today = boardClock.Today;
        var label = BoardPagePolicy.Label(BoardPage.Events);

        var upcoming = await eventRepository.CountUpcomingForBoardAsync(groupId, today, token);
        if (upcoming.Total > 0)
        {
            var noun = upcoming.Total == 1 ? "1 upcoming event" : $"{upcoming.Total} upcoming events";
            var cancelledNote = upcoming.Cancelled > 0 ? $" ({upcoming.Cancelled} of them cancelled)" : string.Empty;
            errors.Add($"{label} can't be switched off while this board has {noun}{cancelledNote}. Delete them, move them to a linked board, or wait until they have passed.");
        }

        var series = await eventRepository.CountActiveSeriesForBoardAsync(groupId, today, token);
        if (series > 0)
        {
            errors.Add(series == 1
                ? $"{label} can't be switched off while 1 recurring series still produces events on this board. End the series first."
                : $"{label} can't be switched off while {series} recurring series still produce events on this board. End each series first.");
        }
    }
}
