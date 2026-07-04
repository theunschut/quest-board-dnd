using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class GroupServiceTests
{
    private readonly IGroupRepository _repository;
    private readonly IMapper _mapper;
    private readonly GroupService _sut;

    public GroupServiceTests()
    {
        _repository = Substitute.For<IGroupRepository>();
        _mapper = Substitute.For<IMapper>();

        _sut = new GroupService(_repository, _mapper);
    }

    // ---------------------------------------------------------------------------
    // GetMembersAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetMembersAsync_DelegatesToRepositoryAndReturnsSameList()
    {
        // Arrange
        var expectedList = new List<UserGroup> { new() { Id = 1, UserId = 1, GroupId = 1 } };
        _repository.GetMembersAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedList);

        // Act
        var result = await _sut.GetMembersAsync(1, "term");

        // Assert
        result.Should().BeSameAs(expectedList);
        await _repository.Received(1).GetMembersAsync(1, "term", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMembersAsync_WhenSearchIsNull_ForwardsNullToRepositoryUnchanged()
    {
        // Arrange
        var expectedList = new List<UserGroup>();
        _repository.GetMembersAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedList);

        // Act
        await _sut.GetMembersAsync(1, null);

        // Assert: the service must not substitute empty-string or filter itself
        await _repository.Received(1).GetMembersAsync(1, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMembersAsync_WhenSearchOmitted_DefaultsToNull()
    {
        // Arrange
        var expectedList = new List<UserGroup>();
        _repository.GetMembersAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedList);

        // Act
        await _sut.GetMembersAsync(1);

        // Assert
        await _repository.Received(1).GetMembersAsync(1, null, Arg.Any<CancellationToken>());
    }
}
