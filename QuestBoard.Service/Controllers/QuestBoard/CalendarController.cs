using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.Extensions;
using QuestBoard.Service.Filters;
using QuestBoard.Service.ViewModels.CalendarViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.QuestBoard;

[Authorize]
[BoardPage(BoardPage.Calendar)]
public class CalendarController(
    IQuestService questService,
    IEventService eventService,
    IEventSeriesService eventSeriesService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IBoardTypeResolver boardTypeResolver,
    IBoardClock boardClock,
    ILinkedCalendarService linkedCalendarService,
    IBoardPageResolver boardPageResolver) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? year = null, int? month = null, CancellationToken token = default)
    {
        // Default to current month if not specified
        var currentDate = boardClock.Now;
        var selectedYear = year ?? currentDate.Year;
        var selectedMonth = month ?? currentDate.Month;

        // Validate month is between 1 and 12
        if (selectedMonth < 1 || selectedMonth > 12)
        {
            return BadRequest("Invalid month. Month must be between 1 and 12.");
        }

        // Validate year is reasonable (between 1900 and 2100)
        if (selectedYear < 1900 || selectedYear > 2100)
        {
            return BadRequest("Invalid year. Year must be between 1900 and 2100.");
        }

        // A campaign board's calendar is an events-only surface, so its quests are never
        // loaded rather than loaded and hidden -- a quest that is never fetched cannot leak
        // through a view, a partial or a future caller. A board type that cannot be resolved
        // keeps the both-kinds behaviour rather than guessing: an unresolved board type means
        // no active board is selected, in which case the fail-closed board query filter
        // already returns no rows, so keeping the behaviour cannot expose anything.
        var activeBoardType = await boardTypeResolver.GetBoardTypeAsync(token);
        var includeQuests = activeBoardType != BoardType.Campaign;

        // The same holds for events: a board that does not use the Events page never loads its own
        // events or its low-runway series, so no link into an event or series page can appear on
        // either layout. The rows stay in the database and come back when the page is switched on.
        var pages = await boardPageResolver.GetAsync(token);
        var includeEvents = pages.Serves(BoardPage.Events);

        // The viewer's tier decides whether DM-only sessions are loaded at all.
        var isDmTier = await IsDmTierAsync();

        // Get all quests with their proposed dates
        IList<Quest> allQuests = includeQuests
            ? await questService.GetQuestsForCalendarAsync(isDmTier, token)
            : [];
        IList<Event> allEvents = includeEvents
            ? await eventService.GetEventsForCalendarAsync(token)
            : [];

        // Create calendar model
        var calendarModel = new CalendarViewModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            Quests = [.. allQuests],
            Events = [.. allEvents],
            CanManage = isDmTier
        };

        // The other boards in this board's linked set add read-only entries for the month shown.
        // The window is built from the requested year and month, never from an ambient clock.
        // A viewer who cannot be resolved gets no overlay rather than a guessed one.
        var viewer = await userService.GetUserAsync(User);
        int? viewerId = viewer.Id == 0 ? null : viewer.Id;
        var monthStart = new DateTime(selectedYear, selectedMonth, 1);
        var linked = await linkedCalendarService.GetForViewerAsync(
            activeGroupContext.ActiveGroupId,
            viewerId,
            User.IsInRole("SuperAdmin"),
            monthStart,
            monthStart.AddMonths(1),
            token);
        calendarModel.LinkedEvents = [.. linked.Events];
        calendarModel.LinkedQuests = [.. linked.Quests];
        calendarModel.LinkedBoards = [.. linked.Boards];

        // A player never triggers the runway query and never receives series titles they
        // have no action to take on.
        if (calendarModel.CanManage && includeEvents)
        {
            calendarModel.SeriesBelowRunway = [.. await eventSeriesService.GetSeriesBelowRunwayAsync(token)];
        }

        return View(calendarModel);
    }

    private async Task<bool> IsDmTierAsync()
    {
        var role = await GetEffectiveRoleAsync();
        return role == GroupRole.Admin || role == GroupRole.DungeonMaster;
    }

    // SuperAdmin has no active group by design, so short-circuit to Admin here rather than
    // calling RequireActiveGroupId(), which would throw for a SuperAdmin with no active group.
    private async Task<GroupRole?> GetEffectiveRoleAsync() =>
        User.IsInRole("SuperAdmin")
            ? GroupRole.Admin
            : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
}