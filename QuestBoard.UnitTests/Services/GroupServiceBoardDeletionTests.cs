using AutoMapper;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardDeletion;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

// The typed-name rules of a board delete, proven at the service: the repository is only reached
// when the typed name matches the stored one exactly (surrounding whitespace aside).
public class GroupServiceBoardDeletionTests
{
    private const int BoardId = 7;
    private const string BoardName = "Iron Table";

    private readonly IGroupRepository _repository = Substitute.For<IGroupRepository>();
    private readonly GroupService _sut;

    public GroupServiceBoardDeletionTests()
    {
        _sut = new GroupService(_repository, Substitute.For<IMapper>());
        _repository.GetByIdAsync(BoardId, Arg.Any<CancellationToken>())
            .Returns(new Group { Id = BoardId, Name = BoardName });
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Delete_WhenTheBoardDoesNotExist_ReturnsNotFoundWithoutDeleting()
    {
        _repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Group?)null);

        var result = await _sut.DeleteBoardAsync(99, BoardName, Token);

        result.Status.Should().Be(BoardDeleteStatus.NotFound);
        await _repository.DidNotReceive().DeleteBoardAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WithTheExactName_CallsTheRepositoryAndReturnsItsResult()
    {
        var deleted = new BoardDeletionResult { Status = BoardDeleteStatus.Deleted, BoardName = BoardName };
        _repository.DeleteBoardAsync(BoardId, Arg.Any<CancellationToken>()).Returns(deleted);

        var result = await _sut.DeleteBoardAsync(BoardId, BoardName, Token);

        result.Should().BeSameAs(deleted);
        await _repository.Received(1).DeleteBoardAsync(BoardId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_TrimsSurroundingWhitespaceFromTheTypedName()
    {
        var deleted = new BoardDeletionResult { Status = BoardDeleteStatus.Deleted, BoardName = BoardName };
        _repository.DeleteBoardAsync(BoardId, Arg.Any<CancellationToken>()).Returns(deleted);

        var result = await _sut.DeleteBoardAsync(BoardId, "  Iron Table  ", Token);

        result.Status.Should().Be(BoardDeleteStatus.Deleted);
        await _repository.Received(1).DeleteBoardAsync(BoardId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("iron table")]
    [InlineData("Iron  Table")]
    [InlineData("Iron Table.")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Delete_WithAnyOtherName_ReturnsNameMismatchWithoutDeleting(string? typed)
    {
        var result = await _sut.DeleteBoardAsync(BoardId, typed, Token);

        result.Status.Should().Be(BoardDeleteStatus.NameMismatch);
        result.BoardName.Should().Be(BoardName);
        await _repository.DidNotReceive().DeleteBoardAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenTheRepositoryReportsMembers_PassesHasMembersThrough()
    {
        var refused = BoardDeletionResult.Refused(BoardDeleteStatus.HasMembers, BoardName);
        _repository.DeleteBoardAsync(BoardId, Arg.Any<CancellationToken>()).Returns(refused);

        var result = await _sut.DeleteBoardAsync(BoardId, BoardName, Token);

        result.Should().BeSameAs(refused);
        result.Status.Should().Be(BoardDeleteStatus.HasMembers);
    }
}
