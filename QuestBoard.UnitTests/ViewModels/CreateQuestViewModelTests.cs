using QuestBoard.Service.ViewModels.QuestViewModels;

namespace QuestBoard.UnitTests.ViewModels;

public class QuestViewModelTests
{
    [Fact]
    public void EditQuestViewModel_ShouldInitializeWithDefaults()
    {
        // Act
        var viewModel = new EditQuestViewModel();

        // Assert
        viewModel.Id.Should().Be(0);
        viewModel.Quest.Should().NotBeNull();
        viewModel.DungeonMasters.Should().NotBeNull();
        viewModel.DungeonMasters.Should().BeEmpty();
        viewModel.CanEditProposedDates.Should().BeFalse();
        viewModel.HasExistingSignups.Should().BeFalse();
    }

    [Fact]
    public void EditQuestViewModel_ShouldSetProperties()
    {
        // Arrange & Act
        var viewModel = new EditQuestViewModel
        {
            Id = 123,
            CanEditProposedDates = true,
            HasExistingSignups = true
        };

        // Assert
        viewModel.Id.Should().Be(123);
        viewModel.CanEditProposedDates.Should().BeTrue();
        viewModel.HasExistingSignups.Should().BeTrue();
    }

    [Fact]
    public void QuestViewModel_ShouldInitialize()
    {
        // Act
        var viewModel = new QuestViewModel();

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.ProposedDates.Should().NotBeNull();
    }
}
