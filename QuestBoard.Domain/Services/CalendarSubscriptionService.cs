using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    IOptions<CalendarFeedOptions> feedOptions,
    ILogger<CalendarSubscriptionService> logger) : ICalendarSubscriptionService
{

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

        var options = feedOptions.Value;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var windowStart = today.AddMonths(-options.MonthsBack);
        var windowEnd = today.AddMonths(options.MonthsAhead);

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

        // A strong fingerprint of the body's own bytes: it changes when and only when the
        // emitted document changes, so an event edit produces a new tag automatically with no
        // modified-timestamp column the schema does not have. The body is composed either way,
        // so this saves nothing server-side -- only transfer, on a document of a few kilobytes
        // polled a handful of times a day. It is not a promise about refresh speed.
        var etag = $"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)))}\"";

        // Touched on every live request, including one that ends in a 304 with no body -- a
        // poll that transferred nothing is still a poll, and this timestamp is the only way to
        // tell a live subscription from a dead one. The throttle above still applies to this
        // write.
        await subscriptionRepository.TouchLastFetchedAsync(
            subscription.Id, timeProvider.GetUtcNow().UtcDateTime, TimeSpan.FromMinutes(options.LastFetchedThrottleMinutes), token);

        return new CalendarFeedResult { Status = CalendarFeedStatus.Ok, Body = body, SubscriptionId = subscription.Id, ETag = etag };
    }
}
