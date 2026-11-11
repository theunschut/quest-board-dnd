namespace QuestBoard.Service.Helpers;

/// <summary>
/// One poster artwork on the desktop quest board: where its files live, the CSS modifier that carries
/// its text box and seal slots, and the size of that measured text box at the full 352px card.
/// </summary>
/// <param name="Key">Short stable name, also the suffix of the CSS modifier class.</param>
/// <param name="CssClass">The modifier class that holds the poster's aspect ratio, insets and seal slots.</param>
/// <param name="PngUrl">Site-relative URL of the original artwork.</param>
/// <param name="WebpUrl">Site-relative URL of the WebP derivative kept beside the PNG.</param>
/// <param name="BoxWidthPx">Width of the measured text box at a 352px card.</param>
/// <param name="BoxHeightPx">Height of the measured text box at a 352px card.</param>
/// <param name="SealOverlapPx">How far the status seal reaches into the bottom of the text box at a 352px card.</param>
public sealed record QuestPoster(
    string Key,
    string CssClass,
    string PngUrl,
    string WebpUrl,
    int BoxWidthPx,
    int BoxHeightPx,
    int SealOverlapPx);

/// <summary>
/// The posters the board may hang a quest on. Each one was measured for the largest clear rectangle on
/// its artwork (a torn edge, a fold or a burn mark makes the rest unusable for text), so the pool is a
/// curated subset of the artwork folder rather than every file in it. The retired tall roll is not in it.
/// </summary>
public static class QuestPosterCatalog
{
    private const string BlanksFolder = "/images/Blanks/Blanks w Shadow/";
    private const string RuinedFolder = "/images/Ruined Posters/Ruined w Shadow/";

    private static QuestPoster Blank(string key, string file, int boxWidth, int boxHeight, int sealOverlap) =>
        new(key, "quest-poster--" + key, BlanksFolder + file + ".png", BlanksFolder + file + ".webp", boxWidth, boxHeight, sealOverlap);

    public static IReadOnlyList<QuestPoster> All { get; } =
    [
        Blank("p1", "Poster1", 222, 408, 43),
        Blank("p2", "Poster2", 220, 225, 10),
        Blank("p4", "Poster4", 183, 243, 39),
        Blank("p5", "Poster5", 205, 241, 11),
        Blank("p6", "Poster6", 205, 264, 0),
        new("r1", "quest-poster--r1", RuinedFolder + "Ruined Poster 1.png", RuinedFolder + "Ruined Poster 1.webp", 218, 180, 0)
    ];

    /// <summary>The poster with the tallest text box.</summary>
    public static QuestPoster Tallest { get; } = All.MaxBy(p => p.BoxHeightPx)!;

    /// <summary>File base names of the four wax seal designs (a PNG and a WebP of each exist).</summary>
    public static IReadOnlyList<string> Seals { get; } = ["Crown Seal", "Eagle Seal", "Flower Seal", "Sigil Seal"];
}
