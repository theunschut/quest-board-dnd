using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class EventService(IEventRepository repository, IMapper mapper) : BaseService<Event>(repository, mapper), IEventService
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
        // Date-only, no time-of-day comparison -- the clock read lives here rather than in the
        // repository so the repository stays testable against a fixed date.
        var today = DateOnly.FromDateTime(DateTime.Today);

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
