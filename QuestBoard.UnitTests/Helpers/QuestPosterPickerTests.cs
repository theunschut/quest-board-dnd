using QuestBoard.Service.Helpers;

namespace QuestBoard.UnitTests.Helpers;

public class QuestPosterPickerTests
{
    private static readonly string[] ApprovedKeys = ["p1", "p2", "p4", "p5", "p6", "r1"];

    private static string TitleOf(int length)
    {
        // Words of mixed length so wrapping behaves like real titles, cut to the exact length.
        const string words = "The Gilded Pact of the Nine Hollowed Crowns Being a True and Wearisome Account of the Salt Queen ";
        var text = string.Concat(Enumerable.Repeat(words, 6));
        return text[..length].TrimEnd().PadRight(length, 'x');
    }

    [Fact]
    public void Pick_SameInputs_ReturnsTheSamePosterTierAndSeal()
    {
        var first = QuestPosterPicker.Pick(37, "The Ember Gate Assize", "Audit Dungeon Master", true, false);
        var second = QuestPosterPicker.Pick(37, "The Ember Gate Assize", "Audit Dungeon Master", true, false);

        second.Should().Be(first);
    }

    [Fact]
    public void Pick_AnyQuest_ReturnsAKeyFromTheApprovedPoolAndNeverPosterThree()
    {
        for (var id = 1; id <= 300; id++)
        {
            foreach (var length in new[] { 5, 40, 90, 160, 200 })
            {
                var pick = QuestPosterPicker.Pick(id, TitleOf(length), "Audit Dungeon Master", id % 2 == 0, id % 7 == 0);

                ApprovedKeys.Should().Contain(pick.Poster.Key);
                pick.Poster.PngUrl.Should().NotContain("Poster3");
            }
        }

        QuestPosterCatalog.All.Select(p => p.Key).Should().BeEquivalentTo(ApprovedKeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pick_TitleOfTheFieldMaximum_GoesToTheTallestPosterAtTheSmallerTier(bool finalizedWithDate)
    {
        var pick = QuestPosterPicker.Pick(12, TitleOf(200), "Audit Dungeon Master", finalizedWithDate, false);

        pick.Poster.Should().Be(QuestPosterCatalog.Tallest);
        pick.TitleTier.Should().Be(2);
    }

    [Fact]
    public void Pick_ResultsAtTierOne_HoldTheirContentInTheBox()
    {
        foreach (var length in new[] { 5, 25, 40, 60 })
        {
            for (var id = 1; id <= 200; id++)
            {
                var finalized = id % 3 == 0;
                var pick = QuestPosterPicker.Pick(id, TitleOf(length), "Audit Dungeon Master", finalized, false);

                if (pick.TitleTier != 1) continue;

                var needed = QuestPosterPicker.EstimateHeight(pick.Poster, TitleOf(length), "Audit Dungeon Master", pick.TitleTier, finalized, false);
                needed.Should().BeLessThanOrEqualTo(QuestPosterPicker.AvailableHeight(pick.Poster),
                    $"quest {id} with a {length}-character title on {pick.Poster.Key}");
            }
        }
    }

    [Fact]
    public void Pick_TwentyCharacterTitleOverTwoHundredQuests_UsesAtLeastFourPostersAndNoneDominates()
    {
        var counts = Enumerable.Range(1, 200)
            .Select(id => QuestPosterPicker.Pick(id, TitleOf(20), "Audit Dungeon Master", false, false).Poster.Key)
            .GroupBy(key => key)
            .ToDictionary(g => g.Key, g => g.Count());

        counts.Count.Should().BeGreaterThanOrEqualTo(4);
        counts.Values.Max().Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void Pick_CampaignQuest_FitsAtLeastAsManyPostersAsAFinalizedQuestWithTheSameTitle()
    {
        var title = TitleOf(60);
        var campaignHeight = QuestPosterPicker.EstimateHeight(QuestPosterCatalog.Tallest, title, "Audit Dungeon Master", 1, false, true);
        var finalizedHeight = QuestPosterPicker.EstimateHeight(QuestPosterCatalog.Tallest, title, "Audit Dungeon Master", 1, true, false);

        campaignHeight.Should().BeLessThan(finalizedHeight);

        var campaignFits = QuestPosterCatalog.All.Count(p => QuestPosterPicker.Holds(p, title, "Audit Dungeon Master", 1, false, true));
        var finalizedFits = QuestPosterCatalog.All.Count(p => QuestPosterPicker.Holds(p, title, "Audit Dungeon Master", 1, true, false));
        campaignFits.Should().BeGreaterThanOrEqualTo(finalizedFits);
    }

    [Fact]
    public void Pick_Seal_IsOneOfTheFourDesignsAndStablePerId()
    {
        var seals = new HashSet<string>();
        for (var id = 1; id <= 100; id++)
        {
            var first = QuestPosterPicker.Pick(id, "A title", "A DM", false, false).Seal;
            var second = QuestPosterPicker.Pick(id, "A title", "A DM", false, false).Seal;

            first.Should().Be(second);
            QuestPosterCatalog.Seals.Should().Contain(first);
            seals.Add(first);
        }

        seals.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Pick_EmptyTitleAndNullDm_DoesNotThrow()
    {
        var act = () => QuestPosterPicker.Pick(1, string.Empty, null, true, false);
        var actNull = () => QuestPosterPicker.Pick(2, null, null, false, true);

        act.Should().NotThrow();
        actNull.Should().NotThrow();
    }

    [Fact]
    public void Pick_LongTitleWithoutSpaces_WrapsByBreakingTheWord()
    {
        var pick = QuestPosterPicker.Pick(5, new string('W', 120), "A DM", false, false);

        pick.TitleTier.Should().Be(2);
        pick.Poster.Should().Be(QuestPosterCatalog.Tallest);
    }

    [Fact]
    public void Holds_DmLineAsWideAsTheBox_CountsTheLineAsWrapped()
    {
        // "DM: Audit Dungeon Master" measures about 190px at 14px and weight 600, which is wider than the
        // 188px box of the ruined poster on the narrowest card, so its second line must be budgeted.
        var ruined = QuestPosterCatalog.All.Single(p => p.Key == "r1");

        var withWrap = QuestPosterPicker.EstimateHeight(ruined, "Ravenfall Crossing", "Audit Dungeon Master", 1, false, false);
        var shortDm = QuestPosterPicker.EstimateHeight(ruined, "Ravenfall Crossing", "Audit DM", 1, false, false);

        withWrap.Should().BeGreaterThan(shortDm);
        QuestPosterPicker.Holds(ruined, "Ravenfall Crossing", "Audit Dungeon Master", 1, false, false).Should().BeFalse();
    }

    [Fact]
    public void Catalog_EveryPoster_HasPngAndWebpUrlsAndAModifierClassMatchingItsKey()
    {
        foreach (var poster in QuestPosterCatalog.All)
        {
            poster.CssClass.Should().Be("quest-poster--" + poster.Key);
            poster.WebpUrl.Should().EndWith(".webp");
            poster.PngUrl.Should().EndWith(".png");
            poster.BoxWidthPx.Should().BeGreaterThan(0);
            poster.BoxHeightPx.Should().BeGreaterThan(0);
        }

        QuestPosterCatalog.Seals.Should().HaveCount(4);
    }
}
