using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.CalendarViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.QuestBoard;

[Authorize]
public class CalendarController(
    IQuestService questService,
    IEventService eventService,
    IEventSeriesService eventSeriesService,
    IUserService userService,
    IActiveGroupContext activeGroupContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? year = null, int? month = null, CancellationToken token = default)
    {
        // Default to current month if not specified
        var currentDate = DateTime.Now;
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

        // Get all quests with their proposed dates
        var allQuests = await questService.GetQuestsForCalendarAsync(token);
        var allEvents = await eventService.GetEventsForCalendarAsync(token);

        // Create calendar model
        var calendarModel = new CalendarViewModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            Quests = [.. allQuests],
            Events = [.. allEvents],
            CanManage = await IsDmTierAsync()
        };

        // A player never triggers the runway query and never receives series titles they
        // have no action to take on.
        if (calendarModel.CanManage)
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