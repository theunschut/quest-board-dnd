using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

public class LinkedBoardSetServiceTests
{
    private readonly ILinkedBoardSetRepository _repository = Substitute.For<ILinkedBoardSetRepository>();
    private readonly LinkedBoardSetService _sut;

    public LinkedBoardSetServiceTests()
    {
        _sut = new LinkedBoardSetService(_repository);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void GivenSet(int id, string name, BoardType boardType, params int[] memberIds)
    {
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(new LinkedBoardSet
        {
            Id = id,
            Name = name,
            BoardType = boardType,
            Boards = memberIds.Select(m => new LinkedBoardSetBoard { Id = m, Name = $"Board {m}" }).ToList()
        });
    }

    private void GivenBoard(int id, string name, BoardType boardType, int? setId = null)
    {
        _repository.GetBoardAsync(id, Arg.Any<CancellationToken>()).Returns(new LinkedBoardCandidate
        {
            Id = id,
            Name = name,
            BoardType = boardType,
            LinkedBoardSetId = setId
        });
    }

    // ---------------------------------------------------------------------------
    // AddBoardAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBoardAsync_WhenBoardTypeDiffersFromSet_ReturnsTypeMismatchAndWritesNothing()
    {
        GivenSet(1, "Friday", BoardType.OneShot, 5, 6);
        GivenBoard(9, "Long Campaign", BoardType.Campaign);

        var outcome = await _sut.AddBoardAsync(1, 9, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.TypeMismatch);
        outcome.BoardName.Should().Be("Long Campaign");
        outcome.BoardType.Should().Be(BoardType.Campaign);
        outcome.SetName.Should().Be("Friday");
        outcome.SetBoardType.Should().Be(BoardType.OneShot);
        await _repository.DidNotReceive().AssignBoardAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddBoardAsync_WhenEmptySetAndBoardOfOtherType_ReturnsTypeMismatchAndWritesNothing()
    {
        GivenSet(1, "Friday", BoardType.OneShot);
        GivenBoard(9, "Long Campaign", BoardType.Campaign);

        var outcome = await _sut.AddBoardAsync(1, 9, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.TypeMismatch);
        await _repository.DidNotReceive().AssignBoardAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AddBoardAsync_WhenBoardAlreadyInASet_ReturnsAlreadyLinkedAndWritesNothing(int existingSetId)
    {
        GivenSet(1, "Friday", BoardType.OneShot, 5);
        GivenBoard(9, "Saturday", BoardType.OneShot, existingSetId);

        var outcome = await _sut.AddBoardAsync(1, 9, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.AlreadyLinked);
        outcome.BoardName.Should().Be("Saturday");
        await _repository.DidNotReceive().AssignBoardAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddBoardAsync_WhenSetOrBoardIsMissing_ReturnsNotFoundAndWritesNothing()
    {
        GivenSet(1, "Friday", BoardType.OneShot);

        var missingBoard = await _sut.AddBoardAsync(1, 404, Token);
        var missingSet = await _sut.AddBoardAsync(404, 9, Token);

        missingBoard.Result.Should().Be(LinkedBoardSetResult.NotFound);
        missingSet.Result.Should().Be(LinkedBoardSetResult.NotFound);
        await _repository.DidNotReceive().AssignBoardAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddBoardAsync_WhenSameTypeAndUnlinked_AssignsTheBoard()
    {
        GivenSet(1, "Friday", BoardType.OneShot);
        GivenBoard(9, "Saturday", BoardType.OneShot);
        _repository.AssignBoardAsync(9, 1, Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await _sut.AddBoardAsync(1, 9, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.Success);
        outcome.SetName.Should().Be("Friday");
        outcome.BoardName.Should().Be("Saturday");
        await _repository.Received(1).AssignBoardAsync(9, 1, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // RemoveBoardAsync
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public async Task RemoveBoardAsync_WhenBoardIsNotInThePostedSet_ReturnsNotInSetAndWritesNothing(int? boardSetId)
    {
        GivenSet(1, "Friday", BoardType.OneShot, 5);
        GivenBoard(9, "Saturday", BoardType.OneShot, boardSetId);

        var outcome = await _sut.RemoveBoardAsync(1, 9, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.NotInSet);
        await _repository.DidNotReceive().AssignBoardAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveBoardAsync_WhenSetOrBoardIsMissing_ReturnsNotFoundAndWritesNothing()
    {
        GivenSet(1, "Friday", BoardType.OneShot, 5);

        var missingBoard = await _sut.RemoveBoardAsync(1, 404, Token);
        var missingSet = await _sut.RemoveBoardAsync(404, 5, Token);

        missingBoard.Result.Should().Be(LinkedBoardSetResult.NotFound);
        missingSet.Result.Should().Be(LinkedBoardSetResult.NotFound);
        await _repository.DidNotReceive().AssignBoardAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveBoardAsync_WhenBoardIsInTheSet_UnlinksIt()
    {
        GivenSet(1, "Friday", BoardType.OneShot, 9);
        GivenBoard(9, "Saturday", BoardType.OneShot, 1);
        _repository.AssignBoardAsync(9, null, Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await _sut.RemoveBoardAsync(1, 9, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.Success);
        outcome.SetName.Should().Be("Friday");
        outcome.BoardName.Should().Be("Saturday");
        await _repository.Received(1).AssignBoardAsync(9, null, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // CreateAsync / RenameAsync validation
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WhenNameIsEmptyOrWhitespace_ReturnsInvalidNameAndWritesNothing(string name)
    {
        var outcome = await _sut.CreateAsync(name, BoardType.OneShot, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.InvalidName);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<BoardType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenNameIsOver100Characters_ReturnsInvalidNameAndWritesNothing()
    {
        var outcome = await _sut.CreateAsync(new string('a', 101), BoardType.OneShot, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.InvalidName);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<BoardType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenNameIsExactly100Characters_Creates()
    {
        var name = new string('a', 100);
        _repository.CreateAsync(name, BoardType.OneShot, Arg.Any<CancellationToken>()).Returns(1);

        var outcome = await _sut.CreateAsync(name, BoardType.OneShot, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.Success);
    }

    [Fact]
    public async Task CreateAsync_WhenBoardTypeIsNotDefined_ReturnsInvalidBoardTypeAndWritesNothing()
    {
        var outcome = await _sut.CreateAsync("Friday", (BoardType)7, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.InvalidBoardType);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<BoardType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenNameMatchesAnExistingSetIgnoringCase_ReturnsDuplicateNameAndWritesNothing()
    {
        _repository.NameExistsAsync("friday", null, Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await _sut.CreateAsync("  friday  ", BoardType.OneShot, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.DuplicateName);
        outcome.SetName.Should().Be("friday");
        await _repository.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<BoardType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenTheUniqueIndexRejectsTheName_ReturnsDuplicateName()
    {
        _repository.NameExistsAsync("Friday", null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.CreateAsync("Friday", BoardType.OneShot, Arg.Any<CancellationToken>()).Returns((int?)null);

        var outcome = await _sut.CreateAsync("Friday", BoardType.OneShot, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.DuplicateName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameAsync_WhenNameIsEmptyOrWhitespace_ReturnsInvalidNameAndWritesNothing(string name)
    {
        var outcome = await _sut.RenameAsync(1, name, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.InvalidName);
        await _repository.DidNotReceive().RenameAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_WhenNameIsOver100Characters_ReturnsInvalidNameAndWritesNothing()
    {
        var outcome = await _sut.RenameAsync(1, new string('a', 101), Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.InvalidName);
        await _repository.DidNotReceive().RenameAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_WhenSetIsMissing_ReturnsNotFoundAndWritesNothing()
    {
        var outcome = await _sut.RenameAsync(404, "Friday", Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.NotFound);
        await _repository.DidNotReceive().RenameAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_WhenAnotherSetHasTheNameIgnoringCase_ReturnsDuplicateNameAndWritesNothing()
    {
        GivenSet(1, "Friday", BoardType.OneShot);
        _repository.NameExistsAsync("SATURDAY", 1, Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await _sut.RenameAsync(1, "SATURDAY", Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.DuplicateName);
        outcome.SetName.Should().Be("SATURDAY");
        await _repository.DidNotReceive().RenameAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_WhenTheRepositoryReportsAClash_ReturnsDuplicateName()
    {
        GivenSet(1, "Friday", BoardType.OneShot);
        _repository.RenameAsync(1, "Saturday", Arg.Any<CancellationToken>()).Returns(false);

        var outcome = await _sut.RenameAsync(1, "Saturday", Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.DuplicateName);
    }

    [Fact]
    public async Task RenameAsync_WhenValid_RenamesWithTheTrimmedName()
    {
        GivenSet(1, "Friday", BoardType.OneShot);
        _repository.RenameAsync(1, "Saturday", Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await _sut.RenameAsync(1, "  Saturday ", Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.Success);
        outcome.SetName.Should().Be("Saturday");
    }

    // ---------------------------------------------------------------------------
    // DeleteAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_WhenSetIsMissing_ReturnsNotFoundAndWritesNothing()
    {
        var outcome = await _sut.DeleteAsync(404, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.NotFound);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenSetExists_DeletesItAndReturnsItsName()
    {
        GivenSet(1, "Friday", BoardType.OneShot, 5, 6);
        _repository.DeleteAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        var outcome = await _sut.DeleteAsync(1, Token);

        outcome.Result.Should().Be(LinkedBoardSetResult.Success);
        outcome.SetName.Should().Be("Friday");
        await _repository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }
}
