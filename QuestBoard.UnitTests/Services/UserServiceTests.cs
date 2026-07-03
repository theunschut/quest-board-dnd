using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Security.Claims;

namespace QuestBoard.UnitTests.Services;

public class UserServiceTests
{
    private readonly IIdentityService _identityService;
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;
    private readonly IGroupService _groupService;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _identityService = Substitute.For<IIdentityService>();
        _repository = Substitute.For<IUserRepository>();
        _mapper = Substitute.For<IMapper>();
        _groupService = Substitute.For<IGroupService>();

        _sut = new UserService(_identityService, _repository, _mapper, _groupService);
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

    // ---------------------------------------------------------------------------
    // CreateOrAddToGroupAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateOrAddToGroupAsync_WhenEmailIsBrandNew_CreatesAccountAndReturnsNewAccountCreated()
    {
        // Arrange
        const string email = "new@example.com";
        const string name = "New Player";
        _identityService.GetIdByEmailAsync(email).Returns((int?)null, 42);
        _identityService.CreateUserAsync(email, name).Returns(IdentityResult.Success);

        // Act
        var result = await _sut.CreateOrAddToGroupAsync(email, name, groupId: 1, GroupRole.Player);

        // Assert
        result.Outcome.Should().Be(CreateOrAddToGroupOutcome.NewAccountCreated);
        result.UserId.Should().Be(42);
        await _repository.Received(1).SetGroupRoleAsync(42, 1, GroupRole.Player);
        await _groupService.DidNotReceive().AddMemberAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GroupRole>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrAddToGroupAsync_WhenExistingConfirmedUserNotAMember_AddsMembershipAndReturnsAddedToGroup()
    {
        // Arrange
        const string email = "existing@example.com";
        var existingUser = new User { Id = 7, Name = "Existing Player", Email = email, EmailConfirmed = true };
        _identityService.GetIdByEmailAsync(email).Returns(7);
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(existingUser);

        // Act
        var result = await _sut.CreateOrAddToGroupAsync(email, "Submitted Name", groupId: 1, GroupRole.Player);

        // Assert
        result.Outcome.Should().Be(CreateOrAddToGroupOutcome.AddedToGroup);
        result.UserId.Should().Be(7);
        result.Name.Should().Be("Existing Player");
        await _groupService.Received(1).AddMemberAsync(1, 7, GroupRole.Player, Arg.Any<CancellationToken>());
        await _identityService.DidNotReceive().CreateUserAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateOrAddToGroupAsync_WhenExistingUnconfirmedUserNotAMember_AddsMembershipAndReturnsAddedToGroupStrandedAccount()
    {
        // Arrange
        const string email = "stranded@example.com";
        var existingUser = new User { Id = 8, Name = "Stranded Player", Email = email, EmailConfirmed = false };
        _identityService.GetIdByEmailAsync(email).Returns(8);
        _repository.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(existingUser);

        // Act
        var result = await _sut.CreateOrAddToGroupAsync(email, "Submitted Name", groupId: 1, GroupRole.Player);

        // Assert
        result.Outcome.Should().Be(CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount);
        result.UserId.Should().Be(8);
        await _groupService.Received(1).AddMemberAsync(1, 8, GroupRole.Player, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrAddToGroupAsync_WhenEmailAlreadyMemberOfGroup_ReturnsAlreadyMember()
    {
        // Arrange
        const string email = "member@example.com";
        var existingUser = new User { Id = 9, Name = "Member Player", Email = email, EmailConfirmed = true };
        _identityService.GetIdByEmailAsync(email).Returns(9);
        _repository.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(existingUser);
        _groupService.AddMemberAsync(1, 9, GroupRole.Player, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("User is already a member of this group."));

        // Act
        var result = await _sut.CreateOrAddToGroupAsync(email, "Submitted Name", groupId: 1, GroupRole.Player);

        // Assert
        result.Outcome.Should().Be(CreateOrAddToGroupOutcome.AlreadyMember);
        result.UserId.Should().Be(9);
        result.Name.Should().Be("Member Player");
        await _identityService.DidNotReceive().CreateUserAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateOrAddToGroupAsync_OnAnyCollisionBranch_NeverCreatesANewAccount()
    {
        // Arrange
        const string email = "collision@example.com";
        var existingUser = new User { Id = 10, Name = "Collision Player", Email = email, EmailConfirmed = true };
        _identityService.GetIdByEmailAsync(email).Returns(10);
        _repository.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(existingUser);

        // Act
        await _sut.CreateOrAddToGroupAsync(email, "Ignored Name", groupId: 1, GroupRole.Player);

        // Assert: the existing account's Name is never touched via a create call
        await _identityService.DidNotReceive().CreateUserAsync(Arg.Any<string>(), Arg.Any<string>());
    }
}
