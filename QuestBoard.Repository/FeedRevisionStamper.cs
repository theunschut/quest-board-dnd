using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

/// <summary>
/// Applies the store-side revision rules for calendar feed entries on every save. An event or a
/// quest carries a revision that rises exactly when a save changes something the calendar feed
/// shows for it, so a calendar client that compares revisions can tell a moved entry from an
/// unchanged one. The revision columns belong to the store: no caller can set them, and this is
/// the only place that writes them after a row is first stored.
/// </summary>
/// <remarks>
/// It reads only the change tracker and issues no query. A write that bypasses the change
/// tracker (a bulk update, raw SQL, or attaching a detached entity) is invisible here, which is
/// why a guard test forbids those shapes in production source.
/// </remarks>
internal static class FeedRevisionStamper
{
    // The properties an event shows in the feed. This list is the contract: the title and the
    // date and start time are the visible entry, the cancellation stamp and the board decide
    // whether the entry is in the feed at all, and the board name prefixes the summary. A
    // property left off this list can be edited without a calendar client ever hearing of it.
    private static readonly string[] EventFeedFields =
    [
        nameof(EventEntity.Title),
        nameof(EventEntity.Date),
        nameof(EventEntity.StartTime),
        nameof(EventEntity.CancelledAt),
        nameof(EventEntity.GroupId),
    ];

    // The properties a quest shows in the feed. The finalized flag and the finalized date decide
    // whether the quest is in the feed and when it starts, so a quest that is reopened and later
    // finalized again comes back under the same identifier with a higher revision.
    private static readonly string[] QuestFeedFields =
    [
        nameof(QuestEntity.Title),
        nameof(QuestEntity.FinalizedDate),
        nameof(QuestEntity.IsFinalized),
        nameof(QuestEntity.GroupId),
    ];

    // Both entity types use the same column names, so these are shared.
    private const string RevisionField = nameof(EventEntity.FeedRevision);
    private const string RevisedAtField = nameof(EventEntity.FeedRevisedAt);
    private const string CreatedAtField = nameof(EventEntity.CreatedAt);

    public static void Apply(ChangeTracker tracker, DateTime utcNow)
    {
        // Mapping a domain model over a tracked entity changes it without telling the tracker,
        // so changes are detected first; otherwise the original and current values would still
        // look identical here.
        tracker.DetectChanges();

        foreach (var entry in tracker.Entries().ToList())
        {
            var feedFields = entry.Entity switch
            {
                EventEntity => EventFeedFields,
                QuestEntity => QuestFeedFields,
                _ => null,
            };

            if (feedFields is null)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    StampNewRow(entry);
                    break;
                case EntityState.Modified:
                    StampChangedRow(entry, feedFields, utcNow);
                    break;
            }
        }
    }

    // A new row starts at revision 1, stamped with its own creation time, so an entry nobody
    // ever edits publishes the same revision number and stamp it always would have.
    private static void StampNewRow(EntityEntry entry)
    {
        var revision = entry.Property(RevisionField);
        if ((int)revision.CurrentValue! < 1)
        {
            revision.CurrentValue = 1;
        }

        var revisedAt = entry.Property(RevisedAtField);
        if ((DateTime)revisedAt.CurrentValue! == default)
        {
            revisedAt.CurrentValue = entry.Property(CreatedAtField).CurrentValue;
        }
    }

    private static void StampChangedRow(EntityEntry entry, string[] feedFields, DateTime utcNow)
    {
        var revision = entry.Property(RevisionField);
        var revisedAt = entry.Property(RevisedAtField);
        var originalRevision = (int)revision.OriginalValue!;
        var originalRevisedAt = (DateTime)revisedAt.OriginalValue!;

        // The store owns both values, so whatever a caller wrote is put back before deciding
        // anything. A bump is therefore always the stored value plus one, never the written one.
        revision.CurrentValue = originalRevision;
        revisedAt.CurrentValue = originalRevisedAt;

        var feedChanged = feedFields.Any(field =>
        {
            var property = entry.Property(field);
            return !Equals(property.OriginalValue, property.CurrentValue);
        });

        if (!feedChanged)
        {
            revision.IsModified = false;
            revisedAt.IsModified = false;
            return;
        }

        revision.CurrentValue = originalRevision + 1;

        // The stamp never moves backwards: if the clock reads earlier than the stored value
        // (clock skew between hosts), the stored stamp stays and the revision still rises.
        revisedAt.CurrentValue = utcNow > originalRevisedAt ? utcNow : originalRevisedAt;
        revision.IsModified = true;
        revisedAt.IsModified = true;
    }
}
