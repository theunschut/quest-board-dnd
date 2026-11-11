namespace QuestBoard.Domain.Extensions;

/// <summary>
/// The one definition of the whitespace that is ignored at the start and end of a board name: when a
/// name is saved, and when the name typed to confirm a board delete is compared with the stored one.
/// </summary>
/// <remarks>
/// The set is every Unicode White_Space character (exactly what <see cref="char.IsWhiteSpace(char)"/>
/// and <see cref="string.Trim()"/> strip) plus U+FEFF, the zero-width no-break space that JavaScript's
/// <c>trim()</c> also strips. The confirmation page's gate script carries the same set character for
/// character, so a name that unlocks the delete button in the browser is always accepted by the server.
/// The comparison itself stays exact: only the edges are trimmed, never case or inner spacing.
/// </remarks>
public static class BoardNameText
{
    /// <summary>The characters trimmed from both ends of a board name, in code point order.</summary>
    public static readonly char[] EdgeWhitespace =
    [
        '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u0020', '\u0085', '\u00A0',
        '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006',
        '\u2007', '\u2008', '\u2009', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F',
        '\u3000', '\uFEFF'
    ];

    /// <summary>
    /// Removes <see cref="EdgeWhitespace"/> from both ends of <paramref name="name"/>; a missing name
    /// becomes the empty string.
    /// </summary>
    public static string TrimEdges(string? name) => name?.Trim(EdgeWhitespace) ?? string.Empty;
}
