namespace QuestBoard.Domain.Services;

/// <summary>
/// Pure, dependency-free cadence arithmetic for a recurring event series. This class takes
/// no constructor, no interface, and no dependency injection, and it never reads the clock -
/// "today" is always supplied by whichever caller needs it. That is deliberate: the live
/// preview, the create-time first generation pass, and the nightly top-up job all call the
/// same static methods here, so it is structurally impossible for the preview to disagree
/// with what actually gets created.
/// </summary>
public static class EventSeriesDateGenerator
{
    /// <summary>
    /// The practical ceiling on cycle-mask length, driven by the storage column's width (one
    /// character plus a comma per position). Independent of, and stricter than, any UI-level
    /// cap the create form applies in the browser - that cap is convenience, this constant is
    /// the enforcement.
    /// </summary>
    public const int MaxCycleLength = 100;

    /// <summary>
    /// The hard iteration ceiling for <see cref="GenerateSlots"/>. No caller-supplied
    /// <c>maxSlots</c> can push a scan past this many slots, so a user-authored cadence can
    /// never drive an unbounded loop in a background job.
    /// </summary>
    public const int MaxSlotScan = 10_000;

    /// <summary>
    /// The single place slot-to-date arithmetic is written. Every cycle-mask position is one
    /// cadence step, not one calendar week, so the date for a slot is the anchor advanced by
    /// that many cadence intervals - never a per-mask-position week offset.
    /// </summary>
    public static DateOnly DateForSlot(DateOnly anchorDate, int intervalWeeks, int slotIndex)
    {
        return anchorDate.AddDays(slotIndex * intervalWeeks * 7);
    }

    /// <summary>
    /// Generates the cadence sequence for a series, one entry per slot, whether or not the
    /// slot fires. The slot index counts every step including non-firing ones, which is what
    /// keeps a slot number permanently stable for the life of the series even after a mask is
    /// interpreted differently or an occurrence is moved - a stable key is the only way an
    /// idempotency check keyed on slot index can work.
    /// </summary>
    /// <param name="anchorDate">The date the cadence is anchored to; every generated date falls on this date's weekday.</param>
    /// <param name="intervalWeeks">The number of weeks between consecutive cadence steps. Must be at least 1.</param>
    /// <param name="cycleMask">The on/off rhythm carved from the cadence grid. Must be non-empty.</param>
    /// <param name="endDate">When set, no slot dated after this value is yielded and generation stops.</param>
    /// <param name="maxSlots">The maximum number of slots to consider, further capped by <see cref="MaxSlotScan"/>.</param>
    public static IEnumerable<(int SlotIndex, DateOnly Date, bool Fires)> GenerateSlots(
        DateOnly anchorDate,
        int intervalWeeks,
        IReadOnlyList<bool> cycleMask,
        DateOnly? endDate,
        int maxSlots)
    {
        if (intervalWeeks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalWeeks), intervalWeeks, "The cadence interval must be at least one week.");
        }

        if (cycleMask is null || cycleMask.Count == 0)
        {
            throw new ArgumentException("The cycle mask must contain at least one position.", nameof(cycleMask));
        }

        // The guard clauses above must run eagerly at call time, not at first enumeration -
        // that is why this method delegates to an iterator instead of being one itself.
        return GenerateSlotsIterator(anchorDate, intervalWeeks, cycleMask, endDate, maxSlots);
    }

    private static IEnumerable<(int SlotIndex, DateOnly Date, bool Fires)> GenerateSlotsIterator(
        DateOnly anchorDate,
        int intervalWeeks,
        IReadOnlyList<bool> cycleMask,
        DateOnly? endDate,
        int maxSlots)
    {
        var slotLimit = Math.Min(maxSlots, MaxSlotScan);
        for (var slot = 0; slot < slotLimit; slot++)
        {
            var date = DateForSlot(anchorDate, intervalWeeks, slot);
            if (endDate.HasValue && date > endDate.Value)
            {
                yield break;
            }

            yield return (slot, date, cycleMask[slot % cycleMask.Count]);
        }
    }

    /// <summary>
    /// Parses a stored cycle-mask string into a list of on/off positions, rejecting anything
    /// that could not have been produced by <see cref="FormatMask"/>. Never returns a
    /// partially-parsed mask - on failure <paramref name="parsed"/> is always empty. Rejecting
    /// an all-zero mask is load-bearing: a mask that never fires would make the top-up job's
    /// firing-slot search scan all the way to <see cref="MaxSlotScan"/> on every run, forever.
    /// </summary>
    public static bool TryParseMask(string? mask, out IReadOnlyList<bool> parsed, out string? error)
    {
        parsed = Array.Empty<bool>();

        if (string.IsNullOrWhiteSpace(mask))
        {
            error = "A cycle must have at least one position.";
            return false;
        }

        var tokens = mask.Split(',');
        if (tokens.Length > MaxCycleLength)
        {
            error = "A cycle can hold at most 100 positions.";
            return false;
        }

        var positions = new bool[tokens.Length];
        var hasFiringPosition = false;
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i].Trim();
            if (token == "1")
            {
                positions[i] = true;
                hasFiringPosition = true;
            }
            else if (token == "0")
            {
                positions[i] = false;
            }
            else
            {
                error = "A cycle position must be on or off.";
                return false;
            }
        }

        if (!hasFiringPosition)
        {
            error = "A cycle must have at least one session position turned on.";
            return false;
        }

        parsed = positions;
        error = null;
        return true;
    }

    /// <summary>
    /// Parses an already-validated, already-persisted cycle-mask string. Callers that have not
    /// already validated the mask should use <see cref="TryParseMask"/> instead.
    /// </summary>
    public static IReadOnlyList<bool> ParseMask(string? mask)
    {
        if (!TryParseMask(mask, out var parsed, out var error))
        {
            throw new ArgumentException(error, nameof(mask));
        }

        return parsed;
    }

    /// <summary>
    /// Renders a cycle mask back to its storage form - the exact form <see cref="TryParseMask"/> accepts.
    /// </summary>
    public static string FormatMask(IReadOnlyList<bool> mask)
    {
        return string.Join(',', mask.Select(position => position ? "1" : "0"));
    }
}
