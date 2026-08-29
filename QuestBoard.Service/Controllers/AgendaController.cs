using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Constants;
using QuestBoard.Service.ViewModels.AgendaViewModels;

namespace QuestBoard.Service.Controllers;

[Authorize]
public class AgendaController(
    IEventService eventService,
    IGroupService groupService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper,
    IOptions<AgendaOptions> agendaOptions) : Controller
{
    // Read-only and scoped entirely by the viewer's own board memberships, which are read
    // fresh from the database on every request below -- membership is the authorisation for
    // this page, so it must never be taken from session or a claim. The page size is clamped
    // server-side the same way the availability overview clamps its own, so a client-supplied
    // value can never turn into an unbounded query; the floor is a second, defensive layer so
    // this clamp still cannot throw even if a host somehow bypasses the configured ceiling.
    [HttpGet]
    public async Task<IActionResult> Index(int? take = null, string? boards = null, CancellationToken token = default)
    {
        var options = agendaOptions.Value;
        var effectiveTake = Math.Clamp(take ?? options.DefaultTake, 1, Math.Max(1, options.MaxTake));

        var currentUser = await userService.GetUserAsync(User);

        // Membership is read fresh on every request and never taken from session or claims,
        // because membership is the authorisation for this page: a board the viewer has left
        // has to disappear on the very next page load, not whenever a cache happens to expire.
        //
        // There is deliberately no SuperAdmin branch here. The board picker hands a SuperAdmin
        // every group; mirroring that on this page would turn it into an unbounded read over
        // every event in the application. One rule for everyone: this page is scoped by the
        // viewer's own membership rows.
        var memberships = await groupService.GetGroupsForUserAsync(currentUser.Id, token);
        var memberGroupIds = memberships.Select(m => m.Id).ToList();

        List<int> requestedIds;
        if (boards == null)
        {
            var stored = HttpContext.Session.GetString(SessionKeys.AgendaBoardFilter);
            requestedIds = stored == null
                ? memberGroupIds
                : stored == "none"
                    ? []
                    : ParseBoardIds(stored);
        }
        else if (string.Equals(boards, "all", StringComparison.OrdinalIgnoreCase))
        {
            // The reset control on the all-filtered-out empty state: clear the remembered
            // selection and fall back to showing every board again.
            HttpContext.Session.Remove(SessionKeys.AgendaBoardFilter);
            requestedIds = memberGroupIds;
        }
        else
        {
            // The desktop filter form carries a leading empty hidden field with the same
            // name, so unticking every box still submits the parameter -- that is how "no
            // boards selected" is expressible at all.
            requestedIds = ParseBoardIds(boards);
        }

        // The stored or supplied selection is only ever a hint about which of the viewer's
        // *current* memberships to show -- it narrows, it can never widen, and a stale id
        // left over from a board the viewer has since left is silently dropped rather than
        // rejected with an error. This is the line that makes "the filter cannot widen the
        // set" true by construction rather than by convention. Do not move it after the
        // query, and do not skip it on any branch.
        var effectiveGroupIds = requestedIds.Intersect(memberGroupIds).Distinct().ToList();

        if (boards != null && !string.Equals(boards, "all", StringComparison.OrdinalIgnoreCase))
        {
            // Store the intersected set, never the raw request, so a foreign id can never be
            // parked in session.
            HttpContext.Session.SetString(
                SessionKeys.AgendaBoardFilter,
                effectiveGroupIds.Count == 0 ? "none" : string.Join(',', effectiveGroupIds));
        }

        var agenda = await eventService.GetCrossBoardAgendaAsync(effectiveGroupIds, currentUser.Id, effectiveTake, token);

        var membershipsById = memberships.ToDictionary(m => m.Id);
        var rows = mapper.Map<IList<AgendaRowViewModel>>(agenda.Rows);
        var renderedRows = new List<AgendaRowViewModel>(rows.Count);
        foreach (var row in rows)
        {
            // Every row's board id is guaranteed present in membershipsById because the
            // service already dropped anything outside the membership set; if a lookup ever
            // misses, drop the row rather than rendering a blank board name.
            if (!membershipsById.TryGetValue(row.BoardId, out var board))
            {
                continue;
            }

            row.BoardName = board.Name;
            row.BoardType = board.BoardType;
            row.IsActiveBoard = activeGroupContext.ActiveGroupId == row.BoardId;
            renderedRows.Add(row);
        }

        var availableBoards = memberships
            .OrderBy(m => m.Name)
            .Select(m => new AgendaBoardOptionViewModel
            {
                Id = m.Id,
                Name = m.Name,
                IsSelected = effectiveGroupIds.Contains(m.Id)
            })
            .ToList();

        string? activeBoardName = activeGroupContext.ActiveGroupId is { } activeId
            && membershipsById.TryGetValue(activeId, out var activeBoard)
                ? activeBoard.Name
                : null;

        // The filtered-out case is kept distinct because it is the only one with a
        // one-click fix, and it is keyed on the effective set being empty rather than on a
        // second probe query, so rendering an empty page never costs an extra unbounded read.
        var emptyState = memberGroupIds.Count == 0
            ? AgendaEmptyState.NoBoards
            : effectiveGroupIds.Count == 0
                ? AgendaEmptyState.AllBoardsFiltered
                : renderedRows.Count == 0
                    ? AgendaEmptyState.NoUpcomingEvents
                    : AgendaEmptyState.None;

        var viewModel = new AgendaViewModel
        {
            Rows = renderedRows,
            AvailableBoards = availableBoards,
            SelectedCount = effectiveGroupIds.Count,
            TotalCount = memberships.Count,
            SelectedBoardIds = string.Join(',', effectiveGroupIds),
            HasMore = agenda.HasMore,
            Take = effectiveTake,
            NextTake = Math.Min(effectiveTake + options.PageIncrement, options.MaxTake),
            CurrentUserId = currentUser.Id,
            ActiveBoardName = activeBoardName,
            EmptyState = emptyState
        };

        return View(viewModel);
    }

    private static List<int> ParseBoardIds(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => int.TryParse(token, out var id) ? (int?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
}
