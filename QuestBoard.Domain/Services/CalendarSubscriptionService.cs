using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class CalendarSubscriptionService(
    ICalendarSubscriptionRepository subscriptionRepository,
    IEventSignupRepository eventSignupRepository,
    IGroupService groupService,
    ICalendarFeedWriter writer,
    TimeProvider timeProvider,
    ILogger<CalendarSubscriptionService> logger) : ICalendarSubscriptionService
{
    // The rolling window's exact bounds move to configuration in a later plan. Fixed here on
    // purpose so this tracer's read path is exercised end to end from the first commit.
    private const int WindowMonthsBack = 3;
    private const int WindowMonthsAhead = 12;

    /// <inheritdoc/>
    public async Task<CalendarSubscription> MintForUserAsync(int userId, CancellationToken token = default)
    {
        // A cryptographic primitive, not Guid.NewGuid() -- this string is the entire
        // authentication mechanism for the feed endpoint, the same trust level as the
        // password-reset token this codebase already generates the same way.
        var bytes = RandomNumberGenerator.GetBytes(32);
        var feedToken = WebEncoders.Base64UrlEncode(bytes);

        // The default name is this application's own list label only -- a person renames it
        // afterwards from Profile.
        return await subscriptionRepository.MintAsync(userId, "New subscription", feedToken, token);
    }

    /// <inheritdoc/>
    public async Task<IList<CalendarSubscription>> GetForUserAsync(int userId, CancellationToken token = default)
        => await subscriptionRepository.GetForUserAsync(userId, token);

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(int subscriptionId, int userId, string name, CancellationToken token = default)
        => await subscriptionRepository.RenameAsync(subscriptionId, userId, name, token);

    /// <inheritdoc/>
    public async Task<bool> RevokeAsync(int subscriptionId, int userId, CancellationToken token = default)
        => await subscriptionRepository.RevokeAsync(subscriptionId, userId, timeProvider.GetUtcNow().UtcDateTime, token);

    /// <inheritdoc/>
    public async Task<CalendarFeedResult> GetFeedAsync(string feedToken, CancellationToken token = default)
    {
        var subscription = await subscriptionRepository.GetByTokenAsync(feedToken, token);
        if (subscription == null)
        {
            return new CalendarFeedResult { Status = CalendarFeedStatus.NotFound };
        }

        if (subscription.IsRevoked)
        {
            return new CalendarFeedResult { Status = CalendarFeedStatus.Revoked, SubscriptionId = subscription.Id };
        }

        // Membership is read fresh from the database on every fetch and never taken from
        // session or claims -- this request has neither. IActiveGroupContext must never be
        // resolved anywhere on this path: it is null here, and every entity filtered through
        // it returns zero rows silently rather than throwing.
        var memberships = await groupService.GetGroupsForUserAsync(subscription.UserId, token);
        var memberGroupIds = memberships.Select(m => m.Id).ToList();
        var boardNamesById = memberships.ToDictionary(m => m.Id, m => m.Name);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var windowStart = today.AddMonths(-WindowMonthsBack);
        var windowEnd = today.AddMonths(WindowMonthsAhead);

        // Called unconditionally, including when memberGroupIds is empty -- a short-circuit
        // here would hide a predicate regression exactly for the caller with no rights, and
        // the repository contract already requires zero rows back for an empty set.
        var fetched = await eventSignupRepository.GetFeedRowsForUserAsync(
            subscription.UserId, memberGroupIds, windowStart, windowEnd, token);

        // Second-layer re-check, mirroring EventService.GetCrossBoardAgendaAsync. This is
        // mandatory here specifically because a feed is read by a machine, so a leak has no
        // reader to notice it.
        var checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId)).ToList();
        if (checkedRows.Count != fetched.Count)
        {
            logger.LogError(
                "Calendar feed dropped {DroppedCount} of {FetchedCount} row(s) falling outside the subscription owner's board set. The query is built from the same set, so this indicates a lost or mistranslated board predicate.",
                fetched.Count - checkedRows.Count,
                fetched.Count);
        }

        var entries = checkedRows
            .Select(row => new CalendarFeedEntry
            {
                Source = CalendarFeedSource.Event,
                SourceId = row.Event.Id,
                BoardName = boardNamesById.TryGetValue(row.Event.GroupId, out var boardName) ? boardName : string.Empty,
                Title = row.Event.Title,
                Date = row.Event.Date,
                StartTime = row.Event.StartTime,
                Availability = row.Availability,
                CreatedAt = row.Event.CreatedAt
            })
            .ToList();

        var body = writer.Write(entries, "D&D Quest Board");

        await subscriptionRepository.TouchLastFetchedAsync(
            subscription.Id, timeProvider.GetUtcNow().UtcDateTime, TimeSpan.Zero, token);

        return new CalendarFeedResult { Status = CalendarFeedStatus.Ok, Body = body, SubscriptionId = subscription.Id };
    }
}
