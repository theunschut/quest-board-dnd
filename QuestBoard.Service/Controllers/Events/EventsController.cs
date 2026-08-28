using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.Service.ViewModels.EventViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Events;

[Authorize]
public class EventsController(
    IEventService eventService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper,
    IEventSignupService eventSignupService,
    IBoardTypeResolver boardTypeResolver,
    IEventSeriesService eventSeriesService) : Controller
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

        var roster = await eventSignupService.GetRosterForEventAsync(id, token);
        viewModel.Roster = mapper.Map<IList<EventSignupViewModel>>(roster);
        viewModel.IsOneShotBoard = await boardTypeResolver.GetBoardTypeAsync(token) == BoardType.OneShot;

        // Derived from the roster we already fetched rather than a second query.
        var ownSignup = roster.FirstOrDefault(s => s.UserId == currentUser.Id);
        viewModel.HasOwnSignup = ownSignup != null;
        viewModel.MyAvailability = ownSignup?.Availability;

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

        // A SuperAdmin has no active group by design, so there is no board to stamp onto the
        // new event. Send them to pick one rather than letting the write throw.
        if (activeGroupContext.ActiveGroupId is not { } activeGroupId)
        {
            return RedirectToAction("Index", "GroupPicker");
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        if (viewModel.IsRecurring)
        {
            return await CreateSeriesAsync(viewModel, activeGroupId, token);
        }

        var newEvent = mapper.Map<Event>(viewModel);

        // The board is taken from the active group context rather than from anything the
        // browser sent, because the read-side query filter offers no protection at all on an
        // insert.
        newEvent.GroupId = activeGroupId;

        var boardType = await boardTypeResolver.GetBoardTypeAsync(token);
        if (boardType == BoardType.Campaign)
        {
            // Every member gets an automatic Yes row, Dungeon Masters and Admins included --
            // a campaign board has no opt-in path, so a role filter would remove them from the
            // feature entirely rather than merely omit them from this one event.
            var members = await userService.GetAllGroupMembersAsync(activeGroupId, token);
            await eventService.AddWithCampaignFanOutAsync(newEvent, members.Select(m => m.Id).ToList(), token);
        }
        else
        {
            await eventService.AddAsync(newEvent, token);
        }

        TempData["Success"] = "Event created successfully.";

        return RedirectToCalendarMonth(newEvent.Date);
    }

    // Split out of the one-off Create branch so that branch stays byte-for-byte the simple
    // path -- a recurring save validates the cadence server-side (the browser's cell cap is
    // convenience, not enforcement) and creates the series plus its whole first generation pass
    // in one transaction, so a mid-save failure leaves nothing behind.
    private async Task<IActionResult> CreateSeriesAsync(EventViewModel viewModel, int activeGroupId, CancellationToken token)
    {
        if (!EventSeriesDateGenerator.TryParseMask(viewModel.CycleMask, out var parsedMask, out var maskError))
        {
            ModelState.AddModelError(nameof(EventViewModel.CycleMask), maskError!);
            return View(viewModel);
        }

        if (viewModel.IntervalWeeks is < 1 or > 52)
        {
            ModelState.AddModelError(nameof(EventViewModel.IntervalWeeks), "Repeat every must be between 1 and 52 weeks");
            return View(viewModel);
        }

        var series = new EventSeries
        {
            Title = viewModel.Title,
            Description = viewModel.Description,
            StartTime = viewModel.StartTime,
            AnchorDate = viewModel.Date,
            IntervalWeeks = viewModel.IntervalWeeks,
            // Normalized through FormatMask so the persisted form is canonical regardless of
            // how the browser wrote the comma-delimited value.
            CycleMask = EventSeriesDateGenerator.FormatMask(parsedMask),
            EndDate = viewModel.SeriesEndDate,
            GroupId = activeGroupId
        };

        try
        {
            var created = await eventSeriesService.CreateWithFirstPassAsync(series, token);
            var occurrences = await eventSeriesService.GetOccurrencesAsync(created.Id, token);
            var redirectDate = occurrences.Count > 0 ? occurrences.Min(occurrence => occurrence.Date) : created.AnchorDate;

            TempData["Success"] = "Series created successfully.";

            return RedirectToCalendarMonth(redirectDate);
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty, "Couldn't create the series. Nothing was saved — check your cadence and try again.");
            return View(viewModel);
        }
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewSeries(SeriesPreviewRequestViewModel request, CancellationToken token = default)
    {
        if (!EventSeriesDateGenerator.TryParseMask(request.CycleMask, out _, out var maskError))
        {
            return Json(new { success = false, error = maskError });
        }

        if (!ModelState.IsValid)
        {
            var intervalError = ModelState[nameof(SeriesPreviewRequestViewModel.IntervalWeeks)]?.Errors.FirstOrDefault()?.ErrorMessage
                ?? "Repeat every must be between 1 and 52 weeks.";
            return Json(new { success = false, error = intervalError });
        }

        var (dates, anchorFullyInPast) = await eventSeriesService.PreviewAsync(
            request.AnchorDate, request.IntervalWeeks, request.CycleMask, request.EndDate, token);

        return Json(new
        {
            success = true,
            dates = dates.Select(date => new
            {
                value = date.ToString("yyyy-MM-dd"),
                label = date.ToDateTime(TimeOnly.MinValue).ToString("dddd, d MMMM yyyy")
            }),
            anchorFullyInPast
        });
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

        // A SuperAdmin has no active group by design, so there is no board to re-stamp onto
        // this event. Send them to pick one rather than letting the write throw.
        if (activeGroupContext.ActiveGroupId is not { } activeGroupId)
        {
            return RedirectToAction("Index", "GroupPicker");
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
        existingEvent.GroupId = activeGroupId;

        await eventService.UpdateAsync(existingEvent, token);

        if (viewModel.EditScope == EventEditScope.ThisAndFutureEvents)
        {
            // A future-scope save is meaningless for a one-off event -- a posted value that
            // cannot be honoured is a malformed request, not something to silently downgrade
            // to the single-event save that already ran above.
            if (!existingEvent.SeriesId.HasValue)
            {
                return BadRequest();
            }

            // Only the title, description and start time propagate forward. The edited
            // occurrence's own date is never pushed onto its siblings, because moving one
            // session must never drag the rest of the series with it.
            await eventSeriesService.ApplyTemplateToFutureAsync(
                existingEvent.SeriesId.Value, id, viewModel.Title, viewModel.Description, viewModel.StartTime, token);

            TempData["Success"] = "This event and all future sessions in the series were updated.";
        }
        else
        {
            TempData["Success"] = "Event updated successfully.";
        }

        return RedirectToCalendarMonth(existingEvent.Date);
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOccurrenceCollision(int id, DateOnly date, CancellationToken token = default)
    {
        var existingEvent = await eventService.GetEventWithDetailsAsync(id, token);
        if (existingEvent == null)
        {
            return NotFound();
        }

        if (!existingEvent.SeriesId.HasValue)
        {
            // A one-off event has no siblings to collide with.
            return Json(new { collision = false });
        }

        if (!await SeriesIsOnActiveBoardAsync(existingEvent.SeriesId.Value, token))
        {
            return BadRequest();
        }

        // Cancelled siblings are deliberately excluded -- a session that is off is not a
        // double booking. The result is advisory only: the move is always allowed, because a
        // double session is legitimate and blocking it would be wrong.
        var collisionCount = await eventSeriesService.CountLiveSiblingsOnDateAsync(
            existingEvent.SeriesId.Value, date, id, token);

        return Json(new
        {
            collision = collisionCount > 0,
            date = date.ToDateTime(TimeOnly.MinValue).ToString("d MMMM")
        });
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

        // A hard delete on a series occurrence would remove the only record that its slot was
        // ever handled, so the nightly generator would recreate it on the next run. Cancel is
        // the only supported way to take a single occurrence off the board -- this refusal is
        // enforced here rather than by hiding the Delete button in the view, because a posted
        // request is not evidence of which button the browser rendered.
        if (existingEvent.SeriesId.HasValue)
        {
            return BadRequest("Delete is not supported for an occurrence of a recurring series. Cancel it instead.");
        }

        // Captured before removal since the redirect target needs the event's date.
        var eventDate = existingEvent.Date;

        await eventService.RemoveAsync(existingEvent, token);

        TempData["Success"] = "Event deleted successfully.";

        return RedirectToCalendarMonth(eventDate);
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken token = default)
    {
        var existingEvent = await eventService.GetEventWithDetailsAsync(id, token);
        if (existingEvent == null)
        {
            return NotFound();
        }

        // Re-resolve the condition on the POST itself rather than trusting which button the
        // browser rendered.
        if (!existingEvent.SeriesId.HasValue)
        {
            return BadRequest("Cancel is only supported for an occurrence of a recurring series.");
        }

        if (!await SeriesIsOnActiveBoardAsync(existingEvent.SeriesId.Value, token))
        {
            return BadRequest();
        }

        await eventService.SetCancelledAsync(id, DateTime.UtcNow, token);

        TempData["Success"] = "Event cancelled.";

        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken token = default)
    {
        var existingEvent = await eventService.GetEventWithDetailsAsync(id, token);
        if (existingEvent == null)
        {
            return NotFound();
        }

        // Re-resolve the condition on the POST itself rather than trusting which button the
        // browser rendered.
        if (!existingEvent.SeriesId.HasValue)
        {
            return BadRequest("Restore is only supported for an occurrence of a recurring series.");
        }

        if (!await SeriesIsOnActiveBoardAsync(existingEvent.SeriesId.Value, token))
        {
            return BadRequest();
        }

        // Un-cancelling is a single write of null and loses nothing, which is the whole reason
        // the cancelled state is a timestamp on the row rather than a deletion.
        await eventService.SetCancelledAsync(id, null, token);

        TempData["Success"] = "Event restored.";

        return RedirectToAction("Details", new { id });
    }

    // After any change the Dungeon Master lands on the calendar showing the month the event is
    // actually in, because dumping them on the current month after creating a January event
    // reads as a silent failure. Year and Month come straight off the date type with no
    // conversion.
    private IActionResult RedirectToCalendarMonth(DateOnly date) =>
        RedirectToAction("Index", "Calendar", new { year = date.Year, month = date.Month });

    // The read filter already hides another board's schedule, and this explicit comparison is
    // a deliberate second layer so a weakened filter still cannot let an event be saved against
    // another board's schedule. With no active group there is no board the series could match,
    // so this fails closed (not permitted) rather than throwing.
    private async Task<bool> SeriesIsOnActiveBoardAsync(int seriesId, CancellationToken token)
    {
        if (activeGroupContext.ActiveGroupId is not { } groupId)
        {
            return false;
        }

        var seriesGroupId = await eventService.GetSeriesGroupIdAsync(seriesId, token);
        return seriesGroupId.HasValue && seriesGroupId.Value == groupId;
    }

    // The read filter already hides another board's events, and this explicit comparison is a
    // deliberate second layer so a weakened filter still cannot let a signup be written against
    // another board's event. With no active board there is nothing to match, so this fails
    // closed (not permitted) rather than throwing.
    private bool EventIsOnActiveBoard(Event candidate) =>
        activeGroupContext.ActiveGroupId is { } groupId && candidate.GroupId == groupId;

    // A player changes their own answer and nobody else's, on either board type, with no
    // Dungeon Master override -- the acting user comes only from currentUser.Id and there is
    // deliberately no user id, member id or signup id parameter on this action.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAvailability(int id, VoteType availability, CancellationToken token = default)
    {
        // The framework's own enum model binder rejects a numeric value with no matching
        // named member and leaves the parameter at its type's default (No) rather than
        // throwing -- without this check, an out-of-range post would silently record a "No"
        // answer nobody actually gave instead of being refused.
        if (!ModelState.IsValid)
        {
            return BadRequest("Invalid availability value.");
        }

        var existingEvent = await eventService.GetEventWithDetailsAsync(id, token);
        if (existingEvent == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser.Id == 0)
        {
            return Challenge();
        }

        if (!Enum.IsDefined(typeof(VoteType), availability))
        {
            return BadRequest("Invalid availability value.");
        }

        if (!EventIsOnActiveBoard(existingEvent))
        {
            return NotFound();
        }

        await eventSignupService.SetAvailabilityAsync(id, currentUser.Id, availability, token);

        return Ok();
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id, CancellationToken token = default)
    {
        var existingEvent = await eventService.GetEventWithDetailsAsync(id, token);
        if (existingEvent == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser.Id == 0)
        {
            return Challenge();
        }

        if (!EventIsOnActiveBoard(existingEvent))
        {
            return NotFound();
        }

        // On a campaign board opting out means changing your own answer to No, not deleting the
        // row, so withdrawing only makes sense on a one-shot board. The board type is
        // re-resolved here rather than trusted from whether the browser rendered the button; a
        // null board type takes this branch too, which is the fail-closed outcome we want.
        var boardType = await boardTypeResolver.GetBoardTypeAsync(token);
        if (boardType != BoardType.OneShot)
        {
            return BadRequest("Withdrawing is only supported on one-shot boards.");
        }

        var removed = await eventSignupService.WithdrawAsync(id, currentUser.Id, token);
        if (!removed)
        {
            return BadRequest("You have not recorded availability for this event.");
        }

        return Ok();
    }

    // The DungeonMasterOnly policy attribute is the security boundary for the write actions.
    // This helper only computes a display flag.
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
