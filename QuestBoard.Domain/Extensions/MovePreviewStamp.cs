using System.Security.Cryptography;
using System.Text;

namespace QuestBoard.Domain.Extensions;

/// <summary>
/// A fingerprint of everything a move would do, shown on the preview and posted back with the
/// confirmation.
/// </summary>
/// <remarks>
/// The stamp only notices that the preview has changed since the page was drawn. It is never an
/// authorization: every confirmation recomputes the whole preview from the store, so a forged
/// or stale stamp can at worst send the user back to the page.
/// </remarks>
public static class MovePreviewStamp
{
    public static string Compute(
        string kind,
        int entryId,
        int targetGroupId,
        IEnumerable<int> kept,
        IEnumerable<int> dropped,
        IEnumerable<int> promoted,
        IEnumerable<int> unlinked,
        IEnumerable<int> added)
    {
        var canonical = string.Join(
            '|',
            kind,
            entryId,
            targetGroupId,
            Canonical(kept),
            Canonical(dropped),
            Canonical(promoted),
            Canonical(unlinked),
            Canonical(added));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Canonical(IEnumerable<int> ids) => string.Join(',', ids.Order());
}
