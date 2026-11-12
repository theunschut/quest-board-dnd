using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the WCAG helper to ratios that were measured by hand on the rendered board before the
/// token layer existed. If the helper drifts from these, the token-contrast guard stops meaning
/// what its thresholds say, so each published figure is asserted to two decimals.
/// </summary>
public class UiFoundationWcagContrastTests
{
    // The lightest pixel of the board backdrop: the worst case for light text on translucent glass.
    private const string LightestBoard = "#794317";
    private const string Glass = "rgba(255,255,255,0.15)";

    // Half a hundredth either side: the published figures are rounded to two decimals.
    private const double Tolerance = 0.005;

    [Fact]
    public void ParchmentOnGlass_AtTheLightestBoardPixel_Is434()
    {
        WcagContrast.TextOn("#F4E4BC", [Glass], LightestBoard).Should().BeApproximately(4.34, Tolerance);
    }

    [Fact]
    public void ParchmentAtSeventyPercentOnGlass_Is299()
    {
        WcagContrast.TextOn("rgba(244,228,188,0.7)", [Glass], LightestBoard).Should().BeApproximately(2.99, Tolerance);
    }

    [Fact]
    public void GoldOnGlass_Is336()
    {
        WcagContrast.TextOn("#ffc107", [Glass], LightestBoard).Should().BeApproximately(3.36, Tolerance);
    }

    [Fact]
    public void RedOnGlass_Is197()
    {
        WcagContrast.TextOn("#ff6b6b", [Glass], LightestBoard).Should().BeApproximately(1.97, Tolerance);
    }

    [Fact]
    public void LightGoldOnGlassOverAThirtyPercentScrim_Is494()
    {
        WcagContrast.TextOn("#ffd54f", ["rgba(26,15,8,0.3)", Glass], LightestBoard)
            .Should().BeApproximately(4.94, Tolerance);
    }

    [Fact]
    public void WhiteOnWood_Is710()
    {
        WcagContrast.TextOn("#ffffff", ["#8B4513"], LightestBoard).Should().BeApproximately(7.10, Tolerance);
    }

    [Fact]
    public void WhiteOnDanger_Is453()
    {
        WcagContrast.TextOn("#ffffff", ["#dc3545"], LightestBoard).Should().BeApproximately(4.53, Tolerance);
    }

    [Fact]
    public void Ratio_OfBlackAndWhite_Is21AndIsSymmetric()
    {
        var black = WcagContrast.Parse("#000");
        var white = WcagContrast.Parse("#fff");

        WcagContrast.Ratio(white, black).Should().BeApproximately(21.0, 1e-9);
        WcagContrast.Ratio(black, white).Should().BeApproximately(21.0, 1e-9);
    }

    [Theory]
    [InlineData("#f80", 255, 136, 0, 1.0)]
    [InlineData("#F4E4BC", 244, 228, 188, 1.0)]
    [InlineData("#f4e4bc80", 244, 228, 188, 128 / 255.0)]
    [InlineData("rgb(10, 20, 30)", 10, 20, 30, 1.0)]
    [InlineData("rgba(10, 20, 30, 0.25)", 10, 20, 30, 0.25)]
    [InlineData("rgb(10 20 30 / 25%)", 10, 20, 30, 0.25)]
    [InlineData("rgba(100%, 0%, 0%, 50%)", 255, 0, 0, 0.5)]
    [InlineData("transparent", 0, 0, 0, 0.0)]
    public void Parse_ReadsEveryColourSyntaxTheTokensUse(
        string input, double r, double g, double b, double a)
    {
        var colour = WcagContrast.Parse(input);

        colour.R.Should().BeApproximately(r, 1e-6);
        colour.G.Should().BeApproximately(g, 1e-6);
        colour.B.Should().BeApproximately(b, 1e-6);
        colour.A.Should().BeApproximately(a, 1e-6);
    }

    [Fact]
    public void Parse_RejectsWhatItCannotRead()
    {
        var notAColour = () => WcagContrast.Parse("var(--qb-fg)");
        notAColour.Should().Throw<FormatException>();
    }

    [Fact]
    public void Over_OfAnOpaqueTopLayer_ReplacesTheBottom()
    {
        var result = WcagContrast.Over(WcagContrast.Parse("#102030"), WcagContrast.Parse("#ffffff"));

        (result.R, result.G, result.B, result.A).Should().Be((16.0, 32.0, 48.0, 1.0));
    }

    [Fact]
    public void Over_OfAHalfLayerOnWhite_AveragesTheChannels()
    {
        var result = WcagContrast.Over(WcagContrast.Parse("rgba(0,0,0,0.5)"), WcagContrast.Parse("#ffffff"));

        result.R.Should().BeApproximately(127.5, 1e-9);
        result.A.Should().BeApproximately(1.0, 1e-9);
    }
}
