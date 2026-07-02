using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using NSubstitute;
using System.Security.Claims;

namespace QuestBoard.UnitTests.Services;

public class UserServiceTests
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _repository = Substitute.For<IUserRepository>();
        _mapper = Substitute.For<IMapper>();

        _sut = new UserService(_identityService, _repository, _mapper);
    }

    private static ClaimsPrincipal MakeSuperAdminPrincipal() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "SuperAdmin")], "test"));

    private static ClaimsPrincipal MakeNonSuperAdminPrincipal() =>
        new(new ClaimsIdentity([], "test"));

    // ---------------------------------------------------------------------------
    // GetEffectiveGroupRoleAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetEffectiveGroupRoleAsync_WhenSuperAdmin_ReturnsAdminWithoutQueryingMembership()
    {
        // Arrange
        var principal = MakeSuperAdminPrincipal();

        // Act
        var result = await _sut.GetEffectiveGroupRoleAsync(principal, 1);

        // Assert: bypass short-circuits before any identity/repository lookup
        result.Should().Be(GroupRole.Admin);
        await _identityService.DidNotReceive().GetUserIdAsync(Arg.Any<ClaimsPrincipal>());
        await _repository.DidNotReceive().GetGroupRoleAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task GetEffectiveGroupRoleAsync_WhenNonSuperAdminAndUnderlyingLookupReturnsAdmin_ReturnsAdmin()
    {
        // Arrange
        var principal = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(principal).Returns(5);
        _repository.GetGroupRoleAsync(5, 1).Returns(GroupRole.Admin);

        // Act
        var result = await _sut.GetEffectiveGroupRoleAsync(principal, 1);

        // Assert
        result.Should().Be(GroupRole.Admin);
    }

    [Fact]
    public async Task GetEffectiveGroupRoleAsync_WhenNonSuperAdminAndUnderlyingLookupReturnsDungeonMaster_ReturnsDungeonMaster()
    {
        // Arrange
        var principal = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(principal).Returns(5);
        _repository.GetGroupRoleAsync(5, 1).Returns(GroupRole.DungeonMaster);

        // Act
        var result = await _sut.GetEffectiveGroupRoleAsync(principal, 1);

        // Assert
        result.Should().Be(GroupRole.DungeonMaster);
    }

    [Fact]
    public async Task GetEffectiveGroupRoleAsync_WhenNonSuperAdminAndNotAMember_ReturnsNull()
    {
        // Arrange
        var principal = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(principal).Returns(5);
        _repository.GetGroupRoleAsync(5, 1).Returns((GroupRole?)null);

        // Act
        var result = await _sut.GetEffectiveGroupRoleAsync(principal, 1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEffectiveGroupRoleAsync_WhenNonSuperAdminAndIdentityLookupReturnsNull_ReturnsNull()
    {
        // Arrange
        var principal = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(principal).Returns((int?)null);

        // Act
        var result = await _sut.GetEffectiveGroupRoleAsync(principal, 1);

        // Assert
        result.Should().BeNull();
        await _repository.DidNotReceive().GetGroupRoleAsync(Arg.Any<int>(), Arg.Any<int>());
    }
}
