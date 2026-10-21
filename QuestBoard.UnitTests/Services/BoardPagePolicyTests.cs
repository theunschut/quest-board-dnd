using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;

namespace QuestBoard.UnitTests.Services;

/// <summary>
/// Pins the page rules every consumer (gate, menus, home redirects, Platform form) shares: the
/// board type ceiling, the Quest Log's dependency on the quest list and the home fallback chain.
/// </summary>
public class BoardPagePolicyTests
{
    private const int MaskCount = 512;

    private static readonly BoardPage[] AllPages = Enum.GetValues<BoardPage>();

    private static readonly BoardPage[] CampaignRefusedPages =
        [BoardPage.Players, BoardPage.Shop, BoardPage.DungeonMasterProfile];

    private static BoardPageSet MaskOf(params BoardPage[] pages)
    {
        var set = BoardPageSet.Empty;
        foreach (var page in pages)
        {
            set = set.With(page);
        }

        return set;
    }

    // -------------------------------------------------------------------
    // Board type ceiling
    // -------------------------------------------------------------------

    [Theory]
    [InlineData(BoardPage.Quests)]
    [InlineData(BoardPage.Calendar)]
    [InlineData(BoardPage.Events)]
    [InlineData(BoardPage.QuestLog)]
    [InlineData(BoardPage.Characters)]
    [InlineData(BoardPage.Contacts)]
    [InlineData(BoardPage.Players)]
    [InlineData(BoardPage.Shop)]
    [InlineData(BoardPage.DungeonMasterProfile)]
    public void AllowedOn_OneShot_IsTrueForEveryPage(BoardPage page)
    {
        BoardPagePolicy.AllowedOn(BoardType.OneShot, page).Should().BeTrue();
    }

    [Theory]
    [InlineData(BoardPage.Quests, true)]
    [InlineData(BoardPage.Calendar, true)]
    [InlineData(BoardPage.Events, true)]
    [InlineData(BoardPage.QuestLog, true)]
    [InlineData(BoardPage.Characters, true)]
    [InlineData(BoardPage.Contacts, true)]
    [InlineData(BoardPage.Players, false)]
    [InlineData(BoardPage.Shop, false)]
    [InlineData(BoardPage.DungeonMasterProfile, false)]
    public void AllowedOn_Campaign_RefusesPlayersShopAndDmProfile(BoardPage page, bool expected)
    {
        BoardPagePolicy.AllowedOn(BoardType.Campaign, page).Should().Be(expected);
    }

    [Fact]
    public void IsAvailable_CampaignBoard_NeverOffersRefusedPagesForAnyStoredMask()
    {
        for (var mask = 0; mask < MaskCount; mask++)
        {
            var disabled = new BoardPageSet(mask);
            foreach (var page in CampaignRefusedPages)
            {
                BoardPagePolicy.IsAvailable(BoardType.Campaign, disabled, page)
                    .Should().BeFalse($"mask {mask} must not widen a Campaign board to {page}");
            }
        }
    }

    [Fact]
    public void IsAvailable_OneShotBoardWithNothingSwitchedOff_OffersEveryPage()
    {
        foreach (var page in AllPages)
        {
            BoardPagePolicy.IsAvailable(BoardType.OneShot, BoardPageSet.Empty, page).Should().BeTrue();
        }
    }

    [Fact]
    public void IsAvailable_SwitchedOffPage_IsUnavailable()
    {
        var disabled = MaskOf(BoardPage.Calendar);

        BoardPagePolicy.IsAvailable(BoardType.OneShot, disabled, BoardPage.Calendar).Should().BeFalse();
        BoardPagePolicy.IsAvailable(BoardType.OneShot, disabled, BoardPage.Events).Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // Quest Log depends on the quest list
    // -------------------------------------------------------------------

    [Theory]
    [InlineData(BoardType.OneShot)]
    [InlineData(BoardType.Campaign)]
    public void IsAvailable_QuestLog_IsUnavailableWhileQuestsIsOff(BoardType type)
    {
        var disabled = MaskOf(BoardPage.Quests);

        disabled.Contains(BoardPage.QuestLog).Should().BeFalse();
        BoardPagePolicy.IsAvailable(type, disabled, BoardPage.QuestLog).Should().BeFalse();
    }

    [Theory]
    [InlineData(BoardType.OneShot)]
    [InlineData(BoardType.Campaign)]
    public void IsAvailable_QuestLog_ComesBackWhenQuestsIsSwitchedBackOnWithItsOwnBitUntouched(BoardType type)
    {
        var withQuestsOff = MaskOf(BoardPage.Quests);

        var restored = withQuestsOff.Without(BoardPage.Quests);

        restored.Contains(BoardPage.QuestLog).Should().BeFalse();
        BoardPagePolicy.IsAvailable(type, restored, BoardPage.QuestLog).Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_QuestLog_StaysUnavailableWhenItsOwnBitIsSet()
    {
        var disabled = MaskOf(BoardPage.QuestLog);

        BoardPagePolicy.IsAvailable(BoardType.OneShot, disabled, BoardPage.QuestLog).Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // Effective home
    // -------------------------------------------------------------------

    [Fact]
    public void EffectiveHome_StoredHomeAvailable_IsTheStoredHome()
    {
        BoardPagePolicy.EffectiveHome(BoardType.OneShot, BoardPageSet.Empty, BoardPage.Calendar)
            .Should().Be(BoardPage.Calendar);
    }

    [Fact]
    public void EffectiveHome_StoredHomeSwitchedOff_FallsBackToQuests()
    {
        BoardPagePolicy.EffectiveHome(BoardType.OneShot, MaskOf(BoardPage.Calendar), BoardPage.Calendar)
            .Should().Be(BoardPage.Quests);
    }

    [Fact]
    public void EffectiveHome_QuestsAndStoredHomeSwitchedOff_FallsBackToTheNextCandidate()
    {
        var disabled = MaskOf(BoardPage.Quests, BoardPage.Calendar);

        BoardPagePolicy.EffectiveHome(BoardType.OneShot, disabled, BoardPage.Calendar)
            .Should().Be(BoardPage.Events);
    }

    [Fact]
    public void EffectiveHome_StoredDmProfile_IsNeverTheHome()
    {
        BoardPagePolicy.EffectiveHome(BoardType.OneShot, BoardPageSet.Empty, BoardPage.DungeonMasterProfile)
            .Should().Be(BoardPage.Quests);
    }

    [Fact]
    public void EffectiveHome_CampaignWithStoredShop_FallsBackToQuests()
    {
        BoardPagePolicy.EffectiveHome(BoardType.Campaign, BoardPageSet.Empty, BoardPage.Shop)
            .Should().Be(BoardPage.Quests);
    }

    [Fact]
    public void EffectiveHome_EveryCandidateSwitchedOff_IsNull()
    {
        var disabled = BoardPageSet.Empty;
        foreach (var candidate in BoardPagePolicy.HomeCandidates)
        {
            disabled = disabled.With(candidate);
        }

        BoardPagePolicy.EffectiveHome(BoardType.OneShot, disabled, BoardPage.Quests).Should().BeNull();
    }

    [Fact]
    public void EffectiveHome_ForEveryBoardTypeMaskAndStoredHome_IsNullOrAnAvailableCandidate()
    {
        foreach (var type in Enum.GetValues<BoardType>())
        {
            for (var mask = 0; mask < MaskCount; mask++)
            {
                var disabled = new BoardPageSet(mask);
                foreach (var storedHome in AllPages)
                {
                    var home = BoardPagePolicy.EffectiveHome(type, disabled, storedHome);

                    if (home is null)
                    {
                        continue;
                    }

                    var context = $"{type}, mask {mask}, stored home {storedHome}";
                    BoardPagePolicy.IsHomeCandidate(home.Value).Should().BeTrue(context);
                    BoardPagePolicy.IsAvailable(type, disabled, home.Value).Should().BeTrue(context);
                    home.Value.Should().NotBe(BoardPage.DungeonMasterProfile, context);
                }
            }
        }
    }

    [Fact]
    public void EffectiveHome_AvailabilityObjectAgreesWithThePolicy()
    {
        var availability = new QuestBoard.Domain.Interfaces.BoardPageAvailability(
            BoardType.OneShot, MaskOf(BoardPage.Calendar), BoardPage.Calendar);

        availability.EffectiveHome.Should().Be(BoardPage.Quests);
        availability.IsAvailable(BoardPage.Calendar).Should().BeFalse();
        availability.IsAvailable(BoardPage.Events).Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // Switchability, labels and lock reasons
    // -------------------------------------------------------------------

    [Theory]
    [InlineData(BoardPage.Calendar)]
    [InlineData(BoardPage.Events)]
    [InlineData(BoardPage.QuestLog)]
    [InlineData(BoardPage.Characters)]
    [InlineData(BoardPage.Contacts)]
    [InlineData(BoardPage.Players)]
    [InlineData(BoardPage.Shop)]
    [InlineData(BoardPage.DungeonMasterProfile)]
    public void IsSwitchable_IsTrueForThePagesAFreshBoardCanLose(BoardPage page)
    {
        BoardPagePolicy.IsSwitchable(page).Should().BeTrue();
    }

    [Fact]
    public void HomeCandidates_ExcludeOnlyTheDmProfile()
    {
        BoardPagePolicy.HomeCandidates.Should().BeEquivalentTo(
            AllPages.Where(p => p != BoardPage.DungeonMasterProfile));
        BoardPagePolicy.Catalogue.Should().BeEquivalentTo(AllPages);
    }

    [Theory]
    [InlineData(BoardPage.Quests, "Quest list")]
    [InlineData(BoardPage.Calendar, "Calendar")]
    [InlineData(BoardPage.Events, "Events")]
    [InlineData(BoardPage.QuestLog, "Quest Log")]
    [InlineData(BoardPage.Characters, "Characters")]
    [InlineData(BoardPage.Contacts, "Contacts")]
    [InlineData(BoardPage.Players, "Players")]
    [InlineData(BoardPage.Shop, "Shop")]
    [InlineData(BoardPage.DungeonMasterProfile, "DM profile")]
    public void Label_ReturnsTheNameShownOnThePlatformForm(BoardPage page, string expected)
    {
        BoardPagePolicy.Label(page).Should().Be(expected);
    }

    [Theory]
    [InlineData(BoardPage.Players)]
    [InlineData(BoardPage.Shop)]
    [InlineData(BoardPage.DungeonMasterProfile)]
    public void LockReason_CampaignBoard_ExplainsTheTypeCeiling(BoardPage page)
    {
        BoardPagePolicy.LockReason(BoardType.Campaign, BoardPageSet.Empty, page)
            .Should().Be("Not available on Campaign boards.");
    }

    [Fact]
    public void LockReason_QuestLogWhileQuestsIsOff_NamesTheQuestList()
    {
        BoardPagePolicy.LockReason(BoardType.OneShot, MaskOf(BoardPage.Quests), BoardPage.QuestLog)
            .Should().Be("Needs the quest list");
    }

    [Fact]
    public void LockReason_UnlockedPage_IsNull()
    {
        BoardPagePolicy.LockReason(BoardType.OneShot, BoardPageSet.Empty, BoardPage.QuestLog).Should().BeNull();
        BoardPagePolicy.LockReason(BoardType.Campaign, BoardPageSet.Empty, BoardPage.Calendar).Should().BeNull();
    }

    // -------------------------------------------------------------------
    // BoardPageSet
    // -------------------------------------------------------------------

    [Fact]
    public void BoardPageSet_WithWithoutAndContains_RoundTripForEveryPage()
    {
        foreach (var page in AllPages)
        {
            var with = BoardPageSet.Empty.With(page);

            with.Contains(page).Should().BeTrue();
            with.Mask.Should().Be(1 << (int)page);
            with.Without(page).Should().Be(BoardPageSet.Empty);

            foreach (var other in AllPages.Where(p => p != page))
            {
                with.Contains(other).Should().BeFalse();
            }
        }
    }
}
