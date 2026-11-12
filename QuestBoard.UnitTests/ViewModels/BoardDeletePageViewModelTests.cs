using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardDeletion;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.UnitTests.ViewModels;

// The confirmation page must show every kind of content the delete removes. A kind the page has no
// label for would otherwise be left off the page while its rows are still deleted.
public class BoardDeletePageViewModelTests
{
    private static Group Board() => new() { Id = 7, Name = "Iron Table", BoardType = BoardType.Campaign };

    [Fact]
    public void Create_ShowsOneRowForEveryContentKind()
    {
        var kinds = Enum.GetValues<BoardContentKind>();
        var preview = new BoardDeletionPreview
        {
            Items = kinds.Select(kind => new BoardDeletionItem { Kind = kind, Count = 1 }).ToList()
        };

        var model = BoardDeletePageViewModel.Create(Board(), preview);

        model.DeletedRows.Should().HaveCount(kinds.Length, "every kind the delete removes needs a row on the page");
        model.DeletedRows.Should().OnlyContain(row => !string.IsNullOrWhiteSpace(row.Text) && !string.IsNullOrWhiteSpace(row.Icon));
    }

    [Fact]
    public void Create_WithAKindThatHasNoLabel_FailsInsteadOfHidingIt()
    {
        var unlabelled = (BoardContentKind)(Enum.GetValues<BoardContentKind>().Cast<int>().Max() + 1);
        var preview = new BoardDeletionPreview
        {
            Items = [new BoardDeletionItem { Kind = unlabelled, Count = 2 }]
        };

        var act = () => BoardDeletePageViewModel.Create(Board(), preview);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{unlabelled}*");
    }

    [Fact]
    public void Create_StillHidesAKindWithNoRows()
    {
        var preview = new BoardDeletionPreview
        {
            Items =
            [
                new BoardDeletionItem { Kind = BoardContentKind.Quests, Count = 0 },
                new BoardDeletionItem { Kind = BoardContentKind.Events, Count = 2 }
            ]
        };

        var model = BoardDeletePageViewModel.Create(Board(), preview);

        model.DeletedRows.Should().ContainSingle().Which.Text.Should().Be("2 events");
    }
}
