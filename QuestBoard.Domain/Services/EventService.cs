using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class EventService(IEventRepository repository, IMapper mapper, TimeProvider timeProvider) : BaseService<Event>(repository, mapper), IEventService
{
    /// <inheritdoc/>
    public async Task<IList<Event>> GetEventsForCalendarAsync(CancellationToken token = default)
    {
        return await repository.GetEventsForCalendarAsync(token);
    }

    /// <inheritdoc/>
    public async Task<Event?> GetEventWithDetailsAsync(int id, CancellationToken token = default)
    {
        return await repository.GetEventWithDetailsAsync(id, token);
    }

    /// <inheritdoc/>
    public async Task<int?> GetSeriesGroupIdAsync(int seriesId, CancellationToken token = default)
    {
        return await repository.GetSeriesGroupIdAsync(seriesId, token);
    }

    /// <inheritdoc/>
    public async Task AddWithCampaignFanOutAsync(Event newEvent, IEnumerable<int> memberIds, CancellationToken token = default)
    {
        await repository.AddWithCampaignFanOutAsync(newEvent, memberIds, token);
    }

    /// <inheritdoc/>
    public async Task<bool> SetCancelledAsync(int eventId, DateTime? cancelledAt, CancellationToken token = default)
    {
        return await repository.SetCancelledAsync(eventId, cancelledAt, token);
    }

    /// <inheritdoc/>
    public async Task<EventAvailabilityOverview> GetAvailabilityOverviewAsync(int take, CancellationToken token = default)
    {
        // Date-only, no time-of-day comparison, and read in UTC from the injected clock so it
        // lines up with the UTC timestamps this same feature already writes onto signups and
        // cancellations.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Asking for one more row than the caller wants is how the page learns there is more to
        // show without a second, separate count query.
        var fetched = await repository.GetUpcomingWithSignupsAsync(today, take + 1, token);
        var hasMore = fetched.Count > take;
        var events = hasMore ? fetched.Take(take).ToList() : fetched.ToList();

        // The member axis is the distinct union of members holding a signup row across the
        // fetched events -- on a board where everyone already holds a row on every event this is
        // every member, and on a board where only some members hold rows it is deliberately just
        // those members. This never queries group membership to double-check the result.
        var members = events
            .SelectMany(e => e.Signups)
            .GroupBy(s => s.UserId)
            .Select(g => new AvailabilityMember { UserId = g.Key, Name = g.First().UserName })
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.UserId)
            .ToList();

        var rows = events.Select(eventWithSignups => BuildRow(eventWithSignups, members)).ToList();

        return new EventAvailabilityOverview
        {
            Members = members,
            Rows = rows,
            HasMore = hasMore
        };
    }

    /// <inheritdoc/>
    public async Task<CrossBoardAgenda> GetCrossBoardAgendaAsync(IReadOnlyCollection<int> memberGroupIds, int currentUserId, int take, CancellationToken token = default)
    {
        // Same clock and date shape as the availability overview: date-only, read in UTC
        // through the injected clock rather than the local system clock.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Called unconditionally, including when memberGroupIds is empty -- a short-circuit
        // here would hide a predicate regression exactly for the caller with no rights, and the
        // repository contract already requires zero rows back for an empty set.
        var fetched = await repository.GetUpcomingAcrossGroupsWithSignupsAsync(memberGroupIds, today, take + 1, token);

        // Second-layer re-check, applied before the window is trimmed so the more-rows flag is
        // computed from surviving rows only. This reads the same memberGroupIds list the query
        // itself was built from, so it catches a dropped predicate or a bad translation of the
        // containment test -- it does not catch a wrong membership set, because both checks
        // trust the same input. It is deliberately weaker than the active-board guard used on
        // the write paths, which compares against independent session state.
        var checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId)).ToList();

        var hasMore = checkedRows.Count > take;
        var windowed = hasMore ? checkedRows.Take(take).ToList() : checkedRows;

        var rows = windowed.Select(row => BuildAgendaRow(row, currentUserId)).ToList();

        return new CrossBoardAgenda
        {
            Rows = rows,
            HasMore = hasMore
        };
    }

    private static AgendaRow BuildAgendaRow(EventWithSignups eventWithSignups, int currentUserId)
    {
        var viewerSignup = eventWithSignups.Signups.FirstOrDefault(s => s.UserId == currentUserId);
        var myCell = viewerSignup == null ? AvailabilityCellState.Empty : ClassifyCell(viewerSignup);

        // Ordered in memory rather than in the query: an ordered include across a take-limited
        // root is not expressible in the single round trip this read is required to be, and
        // this sort runs over a handful of already-materialized rows per row of the agenda.
        var roster = eventWithSignups.Signups
            .OrderBy(s => s.UserName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.UserId)
            .Select(s => new AgendaRosterEntry
            {
                UserId = s.UserId,
                Name = s.UserName,
                Cell = ClassifyCell(s),
                IsViewer = s.UserId == currentUserId
            })
            .ToList();

        return new AgendaRow
        {
            Event = eventWithSignups.Event,
            MyCell = myCell,
            Roster = roster
        };
    }

    private static EventAvailabilityRow BuildRow(EventWithSignups eventWithSignups, IReadOnlyList<AvailabilityMember> members)
    {
        var signupsByUserId = eventWithSignups.Signups.ToDictionary(s => s.UserId);

        var cells = members
            .Select(member => signupsByUserId.TryGetValue(member.UserId, out var signup)
                ? ClassifyCell(signup)
                : AvailabilityCellState.Empty)
            .ToList();

        return new EventAvailabilityRow
        {
            Event = eventWithSignups.Event,
            YesCount = eventWithSignups.Signups.Count(s => s.Availability == VoteType.Yes),
            ConfirmedYesCount = eventWithSignups.Signups.Count(s => s.Availability == VoteType.Yes && s.HasAnswered),
            MaybeCount = eventWithSignups.Signups.Count(s => s.Availability == VoteType.Maybe),
            Cells = cells
        };
    }

    // EventSignup.HasAnswered is the only input to the confirmed/unconfirmed distinction. An
    // unanswered Yes is the one shape that renders differently from its answered counterpart;
    // every other unanswered availability is a data shape that cannot arise from either write
    // path, so it classifies the same as an answered row rather than inventing a sixth state.
    private static AvailabilityCellState ClassifyCell(EventSignup signup)
    {
        if (!signup.HasAnswered && signup.Availability == VoteType.Yes)
        {
            return AvailabilityCellState.UnconfirmedYes;
        }

        return signup.Availability switch
        {
            VoteType.Yes => AvailabilityCellState.ConfirmedYes,
            VoteType.Maybe => AvailabilityCellState.ConfirmedMaybe,
            _ => AvailabilityCellState.ConfirmedNo
        };
    }
}
