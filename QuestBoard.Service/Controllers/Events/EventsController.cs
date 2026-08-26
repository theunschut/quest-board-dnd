using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.EventViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Events;

[Authorize]
public class EventsController(
    IEventService eventService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken token = default)
    {
        var eventEntity = await eventService.GetEventWithDetailsAsync(id, token);
        if (eventEntity == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        var viewModel = mapper.Map<EventViewModel>(eventEntity);
        viewModel.CanManage = currentUser.Id != 0 && await IsDmTierAsync();

        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public IActionResult Create()
    {
        return View(new EventViewModel { Date = DateOnly.FromDateTime(DateTime.Today) });
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventViewModel viewModel, CancellationToken token = default)
    {
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser.Id == 0)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var newEvent = mapper.Map<Event>(viewModel);

        // The board is taken from the active group context rather than from anything the
        // browser sent, because the read-side query filter offers no protection at all on an
        // insert.
        newEvent.GroupId = activeGroupContext.RequireActiveGroupId();

        await eventService.AddAsync(newEvent, token);

        TempData["Success"] = "Event created successfully.";

        return RedirectToCalendarMonth(newEvent.Date);
    }

    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> Edit(int id, CancellationToken token = default)
    {
        var eventEntity = await eventService.GetEventWithDetailsAsync(id, token);
        if (eventEntity == null)
        {
            return NotFound();
        }

        var viewModel = mapper.Map<EventViewModel>(eventEntity);
        viewModel.CanManage = true;

        return View(viewModel);
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventViewModel viewModel, CancellationToken token = default)
    {
        if (id != viewModel.Id)
        {
            return BadRequest();
        }

        var existingEvent = await eventService.GetEventWithDetailsAsync(id, token);
        if (existingEvent == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            viewModel.CanManage = true;
            return View(viewModel);
        }

        if (existingEvent.SeriesId.HasValue && !await SeriesIsOnActiveBoardAsync(existingEvent.SeriesId.Value, token))
        {
            return BadRequest();
        }

        existingEvent.Title = viewModel.Title;
        existingEvent.Description = viewModel.Description;
        existingEvent.Date = viewModel.Date;
        existingEvent.StartTime = viewModel.StartTime;

        // The board is re-derived on every write rather than round-tripped through the form.
        existingEvent.GroupId = activeGroupContext.RequireActiveGroupId();

        await eventService.UpdateAsync(existingEvent, token);

        TempData["Success"] = "Event updated successfully.";

        return RedirectToCalendarMonth(existingEvent.Date);
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken token = default)
    {
        var existingEvent = await eventService.GetEventWithDetailsAsync(id, token);
        if (existingEvent == null)
        {
            return NotFound();
        }

        // Captured before removal since the redirect target needs the event's date.
        var eventDate = existingEvent.Date;

        await eventService.RemoveAsync(existingEvent, token);

        TempData["Success"] = "Event deleted successfully.";

        return RedirectToCalendarMonth(eventDate);
    }

    // After any change the Dungeon Master lands on the calendar showing the month the event is
    // actually in, because dumping them on the current month after creating a January event
    // reads as a silent failure. Year and Month come straight off the date type with no
    // conversion.
    private IActionResult RedirectToCalendarMonth(DateOnly date) =>
        RedirectToAction("Index", "Calendar", new { year = date.Year, month = date.Month });

    // The read filter already hides another board's schedule, and this explicit comparison is
    // a deliberate second layer so a weakened filter still cannot let an event be saved against
    // another board's schedule.
    private async Task<bool> SeriesIsOnActiveBoardAsync(int seriesId, CancellationToken token)
    {
        var seriesGroupId = await eventService.GetSeriesGroupIdAsync(seriesId, token);
        return seriesGroupId.HasValue && seriesGroupId.Value == activeGroupContext.RequireActiveGroupId();
    }

    // The DungeonMasterOnly policy attribute is the security boundary for the write actions.
    // This helper only computes a display flag.
    private async Task<bool> IsDmTierAsync()
    {
        var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
        return role == GroupRole.Admin || role == GroupRole.DungeonMaster;
    }
}
