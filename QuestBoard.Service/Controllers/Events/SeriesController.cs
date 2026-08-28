using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;
using QuestBoard.Service.ViewModels.SeriesViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Events;

[Authorize]
public class SeriesController(
    IEventSeriesService eventSeriesService,
    IEventService eventService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken token = default)
    {
        var series = await eventSeriesService.GetSeriesAsync(id, token);
        if (series == null)
        {
            return NotFound();
        }

        var viewModel = mapper.Map<SeriesDetailsViewModel>(series);

        // Filled from the Domain parser rather than a second parse written in the view, so the
        // read-only strip renders the same rule the generator uses.
        viewModel.CyclePositions = EventSeriesDateGenerator.ParseMask(series.CycleMask).ToList();

        var occurrences = await eventSeriesService.GetOccurrencesAsync(id, token);
        viewModel.Occurrences = mapper.Map<IList<SeriesOccurrenceViewModel>>(occurrences);

        viewModel.CanManage = await IsDmTierAsync();

        // The removal-impact counts are management information; there is no reason to compute
        // or send them to a player.
        if (viewModel.CanManage)
        {
            var impact = await eventSeriesService.GetRemovalImpactAsync(id, token);
            viewModel.PastCount = impact.PastCount;
            viewModel.FutureCount = impact.FutureCount;
            viewModel.AnsweredCount = impact.AnsweredCount;
        }

        return View(viewModel);
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id, CancellationToken token = default)
    {
        if (!await SeriesIsOnActiveBoardAsync(id, token))
        {
            return BadRequest();
        }

        // Ending sets a date rather than a flag, so the same column also expresses a
        // fixed-length arc declared at setup time on the create form. The confirm the DM just
        // accepted states that upcoming sessions will be cleared, so the clearing is part of the
        // same action rather than a second step. Sessions dated today or earlier are always
        // kept -- they record sessions that happened.
        await eventSeriesService.EndAsync(id, DateOnly.FromDateTime(DateTime.Today), removeFutureOccurrences: true, token);

        TempData["Success"] = "Series ended successfully.";

        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken token = default)
    {
        if (!await SeriesIsOnActiveBoardAsync(id, token))
        {
            return BadRequest();
        }

        // Every occurrence goes with the series and their availability rows follow through the
        // shipped cascade.
        await eventSeriesService.DeleteAsync(id, token);

        TempData["Success"] = "Series deleted successfully.";

        return RedirectToAction("Index", "Calendar");
    }

    [HttpPost]
    [Authorize(Policy = "DungeonMasterOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Detach(int id, CancellationToken token = default)
    {
        if (!await SeriesIsOnActiveBoardAsync(id, token))
        {
            return BadRequest();
        }

        // Only the rule is removed; every session stays as an ordinary one-off event, which
        // serves the case where the recurrence was wrong but the sessions that were played
        // should stay.
        await eventSeriesService.DetachAsync(id, token);

        TempData["Success"] = "Series detached — its sessions are now one-off events.";

        return RedirectToAction("Index", "Calendar");
    }

    // The read filter already hides another board's schedule, and this explicit comparison is
    // a deliberate second layer so a weakened filter still cannot let a write land on another
    // board's series. With no active group there is nothing to match, so this fails closed (not
    // permitted) rather than throwing.
    private async Task<bool> SeriesIsOnActiveBoardAsync(int seriesId, CancellationToken token)
    {
        if (activeGroupContext.ActiveGroupId is not { } groupId)
        {
            return false;
        }

        var seriesGroupId = await eventService.GetSeriesGroupIdAsync(seriesId, token);
        return seriesGroupId.HasValue && seriesGroupId.Value == groupId;
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
