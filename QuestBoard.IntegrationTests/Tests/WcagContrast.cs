using System.Globalization;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// WCAG 2.x colour maths for the static contrast guards: parse a CSS colour, composite translucent
/// layers over an opaque board colour, and measure the contrast ratio between two opaque colours.
/// A text-shadow never counts toward a ratio, so nothing here knows about one.
/// </summary>
internal static class WcagContrast
{
    /// <summary>A colour with channels 0 to 255 and alpha 0 to 1.</summary>
    internal readonly record struct Rgba(double R, double G, double B, double A)
    {
        public bool IsOpaque => A >= 1.0 - 1e-9;
    }

    private static readonly Regex Functional = new(
        @"^rgba?\(\s*(?<args>[^)]*?)\s*\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>transparent</c> and
    /// <c>rgb()</c>/<c>rgba()</c> in comma or space syntax, with the alpha as a number or a percentage.
    /// </summary>
    internal static Rgba Parse(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            return new Rgba(0, 0, 0, 0);

        if (text.StartsWith('#'))
            return ParseHex(text);

        var functional = Functional.Match(text);
        if (functional.Success)
            return ParseFunctional(functional.Groups["args"].Value, text);

        throw new FormatException($"'{value}' is not a colour literal this helper understands.");
    }

    private static Rgba ParseHex(string text)
    {
        var digits = text[1..];
        if (!Regex.IsMatch(digits, "^[0-9a-fA-F]+$") || digits.Length is not (3 or 4 or 6 or 8))
            throw new FormatException($"'{text}' is not a valid hex colour.");

        if (digits.Length <= 4)
            digits = string.Concat(digits.Select(c => new string(c, 2)));

        double Channel(int index) =>
            int.Parse(digits.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var alpha = digits.Length == 8 ? Channel(3) / 255.0 : 1.0;
        return new Rgba(Channel(0), Channel(1), Channel(2), alpha);
    }

    private static Rgba ParseFunctional(string args, string original)
    {
        var parts = Regex.Split(args.Trim(), @"\s*[,/]\s*|\s+")
            .Where(p => p.Length > 0)
            .ToArray();
        if (parts.Length is not (3 or 4))
            throw new FormatException($"'{original}' needs three channels and an optional alpha.");

        double Channel(string part) =>
            part.EndsWith('%')
                ? Math.Clamp(Number(part[..^1]) * 2.55, 0, 255)
                : Math.Clamp(Number(part), 0, 255);

        double Alpha(string part) =>
            part.EndsWith('%')
                ? Math.Clamp(Number(part[..^1]) / 100.0, 0, 1)
                : Math.Clamp(Number(part), 0, 1);

        double Number(string part) =>
            double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                ? n
                : throw new FormatException($"'{part}' in '{original}' is not a number.");

        return new Rgba(Channel(parts[0]), Channel(parts[1]), Channel(parts[2]), parts.Length == 4 ? Alpha(parts[3]) : 1.0);
    }

    /// <summary>Source-over compositing in sRGB: <paramref name="top"/> laid on <paramref name="bottom"/>.</summary>
    internal static Rgba Over(Rgba top, Rgba bottom)
    {
        var alpha = top.A + bottom.A * (1 - top.A);
        if (alpha <= 0)
            return new Rgba(0, 0, 0, 0);

        double Mix(double t, double b) => (t * top.A + b * bottom.A * (1 - top.A)) / alpha;
        return new Rgba(Mix(top.R, bottom.R), Mix(top.G, bottom.G), Mix(top.B, bottom.B), alpha);
    }

    /// <summary>Relative luminance per WCAG 2.x, of the colour's channels; alpha is ignored.</summary>
    internal static double RelativeLuminance(Rgba colour)
    {
        static double Linear(double channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linear(colour.R) + 0.7152 * Linear(colour.G) + 0.0722 * Linear(colour.B);
    }

    /// <summary>Contrast ratio between two opaque colours, always 1 or more.</summary>
    internal static double Ratio(Rgba a, Rgba b)
    {
        if (!a.IsOpaque || !b.IsOpaque)
            throw new ArgumentException("Contrast is only defined between opaque colours; composite the layers first.");

        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>
    /// The surface a stack of translucent layers makes: <paramref name="layers"/> are listed bottom
    /// first and composited over the opaque <paramref name="board"/> colour.
    /// </summary>
    internal static Rgba Surface(IEnumerable<string> layers, string board) =>
        layers.Aggregate(Parse(board), (under, layer) => Over(Parse(layer), under));

    /// <summary>
    /// The contrast of <paramref name="text"/> on the surface the <paramref name="layers"/> make over
    /// <paramref name="board"/>. A translucent text colour is composited over that surface first.
    /// </summary>
    internal static double TextOn(string text, IEnumerable<string> layers, string board)
    {
        var surface = Surface(layers, board);
        return Ratio(Over(Parse(text), surface), surface);
    }
}
