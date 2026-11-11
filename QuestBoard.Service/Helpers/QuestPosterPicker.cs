namespace QuestBoard.Service.Helpers;

/// <summary>The poster, title size and seal design chosen for one quest on the board.</summary>
/// <param name="Poster">The artwork the quest hangs on.</param>
/// <param name="TitleTier">1 for the large title, 2 for the smaller one used only when no poster holds the title.</param>
/// <param name="Seal">Base name of the wax seal design.</param>
public sealed record QuestPosterChoice(QuestPoster Poster, int TitleTier, string Seal);

/// <summary>
/// Chooses a poster for a quest. The choice is a pure function of the quest's id, its title and DM name,
/// whether it is finalized with a date and whether it sits on a campaign board, so the same quest keeps
/// the same poster and seal on every visit and for every viewer until one of those changes. Editing the
/// title or finalizing a quest can therefore move it to another poster. Only posters whose measured text
/// box is estimated to hold the content at the large title size are candidates. A title too long for
/// every poster is set at the smaller size on the poster with the most room left for it, wraps and is
/// never truncated; the estimate is an average over measured glyphs, so a caps-heavy or very long title
/// can still run past the box on that poster.
/// </summary>
public static class QuestPosterPicker
{
    // Cinzel at weight 600 measured 0.578 to 0.633 em per character; the body face 0.507 to 0.564 em (the
    // widest was a 24-character DM line, which a 189px box just fails to hold on one line). Both are rounded
    // up so the estimate errs towards a taller poster.
    private const double TitleEm = 0.64;
    private const double BodyEm = 0.57;
    private const double BodyPx = 14;

    // The board never draws a card narrower than this, and the estimate uses the same width, so a pick
    // that fits here also fits on a wider card (boxes grow with the card, the 14px lines do not).
    private const double EstimateCardWidth = 304;
    private const double FullCardWidth = 352;

    private const double TitleGap = 8;
    private const double DmLine = 21;

    // Height of everything under the title for each state, at the 14px floor with 1.5 line height.
    private const double OpenMeta = 94;
    private const double FinalizedMeta = 144;
    private const double CampaignMeta = 29;

    private static double TitlePx(int tier) => tier == 1 ? 20 : 16;
    private static double TitleLine(int tier) => tier == 1 ? 24 : 19.2;

    /// <summary>
    /// Picks the poster, title tier and seal for a quest. A title or DM name that is null is treated as empty.
    /// </summary>
    public static QuestPosterChoice Pick(int questId, string? title, string? dmName, bool finalizedWithDate, bool campaign)
    {
        var mixed = Mix(questId);
        var seal = QuestPosterCatalog.Seals[Reduce(Mix2(mixed), QuestPosterCatalog.Seals.Count)];

        var fits = QuestPosterCatalog.All
            .Where(poster => Holds(poster, title, dmName, 1, finalizedWithDate, campaign))
            .ToList();

        if (fits.Count > 0)
        {
            return new QuestPosterChoice(fits[Reduce(mixed, fits.Count)], 1, seal);
        }

        return new QuestPosterChoice(RoomiestAtSmallerTitle(title, dmName, finalizedWithDate, campaign), 2, seal);
    }

    // When no poster holds the content at the large title, the one with the most height left over (or the
    // least short of it) at the smaller title gets the quest. A taller box breaks a tie.
    private static QuestPoster RoomiestAtSmallerTitle(string? title, string? dmName, bool finalizedWithDate, bool campaign) =>
        QuestPosterCatalog.All
            .OrderByDescending(poster => AvailableHeight(poster) - EstimateHeight(poster, title, dmName, 2, finalizedWithDate, campaign))
            .ThenByDescending(poster => poster.BoxHeightPx)
            .First();

    /// <summary>Whether the poster's text box holds the quest's content at the given title tier.</summary>
    public static bool Holds(QuestPoster poster, string? title, string? dmName, int tier, bool finalizedWithDate, bool campaign) =>
        EstimateHeight(poster, title, dmName, tier, finalizedWithDate, campaign) <= AvailableHeight(poster);

    /// <summary>The height of the text box that is free of the status seal, at the estimate width.</summary>
    public static double AvailableHeight(QuestPoster poster) =>
        (poster.BoxHeightPx - poster.SealOverlapPx) * Scale;

    /// <summary>The estimated height of the content: wrapped title, then the meta lines for the quest's state.</summary>
    public static double EstimateHeight(QuestPoster poster, string? title, string? dmName, int tier, bool finalizedWithDate, bool campaign)
    {
        var boxWidth = poster.BoxWidthPx * Scale;
        var titleLines = WrapLines(title ?? string.Empty, boxWidth, TitlePx(tier), TitleEm);
        var dmLines = WrapLines("DM: " + (string.IsNullOrWhiteSpace(dmName) ? "Unknown DM" : dmName), boxWidth, BodyPx, BodyEm);
        var meta = campaign ? CampaignMeta : finalizedWithDate ? FinalizedMeta : OpenMeta;

        return titleLines * TitleLine(tier) + TitleGap + meta + (dmLines - 1) * DmLine;
    }

    private static double Scale => EstimateCardWidth / FullCardWidth;

    // Greedy word wrap at a fixed advance per character; a word longer than the line is broken.
    private static int WrapLines(string text, double boxWidth, double px, double em)
    {
        var max = Math.Max(1, (int)Math.Floor(boxWidth / (em * px)));
        var lines = 1;
        var current = 0;

        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length > max)
            {
                if (current > 0) lines++;
                lines += (int)Math.Ceiling(word.Length / (double)max) - 1;
                current = word.Length % max == 0 ? max : word.Length % max;
            }
            else if (current == 0)
            {
                current = word.Length;
            }
            else if (current + 1 + word.Length <= max)
            {
                current += 1 + word.Length;
            }
            else
            {
                lines++;
                current = word.Length;
            }
        }

        return lines;
    }

    // A multiplicative mix of the id. The built-in string hash is randomised per process, so it would give a
    // quest a different poster after every restart.
    private static uint Mix(int id) => unchecked((uint)id * 2654435761u);

    // A second, differently shifted mix so the seal design is independent of the poster choice.
    private static uint Mix2(uint mixed) => unchecked((mixed ^ (mixed >> 16)) * 2246822519u);

    // Range reduction on the high bits: the low bits of a multiplicative hash only carry the id's parity.
    private static int Reduce(uint mixed, int count) => (int)(((ulong)mixed * (ulong)count) >> 32);
}
