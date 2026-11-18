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

    // ---------------------------------------------------------------------------
    // ChangeMemberRoleAsync
    // ---------------------------------------------------------------------------

    private void GivenBoard(int groupId, int targetId, GroupRole? targetRole, int admins)
    {
        _repository.GetGroupRoleAsync(targetId, groupId).Returns(targetRole);
        _groupService.CountMembersWithRoleAsync(groupId, GroupRole.Admin, Arg.Any<CancellationToken>()).Returns(admins);
    }

    private async Task AssertNothingWrittenAsync() =>
        await _repository.DidNotReceive().SetGroupRoleAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GroupRole>());

    [Theory]
    [InlineData(GroupRole.Admin, GroupRole.DungeonMaster)]
    [InlineData(GroupRole.Admin, GroupRole.Player)]
    [InlineData(GroupRole.DungeonMaster, GroupRole.Admin)]
    [InlineData(GroupRole.Player, GroupRole.DungeonMaster)]
    public async Task ChangeMemberRoleAsync_WhenActorChangesTheirOwnRole_ReturnsSelfAndWritesNothing(GroupRole current, GroupRole requested)
    {
        GivenBoard(1, 7, current, admins: 2);

        var result = await _sut.ChangeMemberRoleAsync(1, actingUserId: 7, targetUserId: 7, requested, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.Self);
        await AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData(GroupRole.DungeonMaster)]
    [InlineData(GroupRole.Player)]
    public async Task ChangeMemberRoleAsync_WhenTargetIsTheOnlyAdminAndWouldNoLongerBeOne_ReturnsLastAdminWhoeverActs(GroupRole requested)
    {
        // The actor is a different account (for example a platform administrator), so only the resulting role matters.
        GivenBoard(1, 7, GroupRole.Admin, admins: 1);

        var result = await _sut.ChangeMemberRoleAsync(1, actingUserId: 99, targetUserId: 7, requested, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.LastAdmin);
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAnotherAdminRemains_DemotesTheAdminAndReturnsChanged()
    {
        GivenBoard(1, 7, GroupRole.Admin, admins: 2);

        var result = await _sut.ChangeMemberRoleAsync(1, actingUserId: 99, targetUserId: 7, GroupRole.DungeonMaster, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.Changed);
        await _repository.Received(1).SetGroupRoleAsync(7, 1, GroupRole.DungeonMaster);
    }

    [Theory]
    [InlineData(GroupRole.Player, GroupRole.Admin)]
    [InlineData(GroupRole.Player, GroupRole.DungeonMaster)]
    [InlineData(GroupRole.DungeonMaster, GroupRole.Admin)]
    [InlineData(GroupRole.DungeonMaster, GroupRole.Player)]
    public async Task ChangeMemberRoleAsync_WhenTargetIsNotAnAdmin_ReturnsChangedWithoutCountingAdmins(GroupRole current, GroupRole requested)
    {
        GivenBoard(1, 7, current, admins: 1);

        var result = await _sut.ChangeMemberRoleAsync(1, actingUserId: 99, targetUserId: 7, requested, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.Changed);
        await _repository.Received(1).SetGroupRoleAsync(7, 1, requested);
        await _groupService.DidNotReceive().CountMembersWithRoleAsync(Arg.Any<int>(), Arg.Any<GroupRole>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenTargetStaysAnAdmin_ReturnsChangedEvenWhenOnlyOne()
    {
        GivenBoard(1, 7, GroupRole.Admin, admins: 1);

        var result = await _sut.ChangeMemberRoleAsync(1, actingUserId: 99, targetUserId: 7, GroupRole.Admin, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.Changed);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenTargetIsNotAMember_ReturnsNotAMemberAndWritesNothing()
    {
        GivenBoard(1, 7, null, admins: 1);

        var result = await _sut.ChangeMemberRoleAsync(1, actingUserId: 99, targetUserId: 7, GroupRole.Admin, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.NotAMember);
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenActorIsNotAMemberAndTargetIsAnother_IsNeverSelf()
    {
        // A platform administrator who is not on the board has an id that never matches a member's.
        GivenBoard(1, 7, GroupRole.Player, admins: 1);

        var result = await _sut.ChangeMemberRoleAsync(1, actingUserId: 1000, targetUserId: 7, GroupRole.DungeonMaster, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.Changed);
    }

    // ---------------------------------------------------------------------------
    // TryRemoveMemberAsync
    // ---------------------------------------------------------------------------

    private async Task AssertNobodyRemovedAsync() =>
        await _groupService.DidNotReceive().RemoveMemberAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

    [Theory]
    [InlineData(GroupRole.Admin)]
    [InlineData(GroupRole.DungeonMaster)]
    [InlineData(GroupRole.Player)]
    public async Task TryRemoveMemberAsync_WhenActorRemovesThemselves_ReturnsSelfAndRemovesNobody(GroupRole current)
    {
        GivenBoard(1, 7, current, admins: 2);

        var result = await _sut.TryRemoveMemberAsync(1, actingUserId: 7, targetUserId: 7, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.Self);
        await AssertNobodyRemovedAsync();
    }

    [Fact]
    public async Task TryRemoveMemberAsync_WhenTargetIsTheOnlyAdmin_ReturnsLastAdminWhoeverActsAndRemovesNobody()
    {
        GivenBoard(1, 7, GroupRole.Admin, admins: 1);

        var result = await _sut.TryRemoveMemberAsync(1, actingUserId: 99, targetUserId: 7, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.LastAdmin);
        await AssertNobodyRemovedAsync();
    }

    [Fact]
    public async Task TryRemoveMemberAsync_WhenAnotherAdminRemains_RemovesTheAdminAndReturnsChanged()
    {
        GivenBoard(1, 7, GroupRole.Admin, admins: 2);

        var result = await _sut.TryRemoveMemberAsync(1, actingUserId: 99, targetUserId: 7, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.Changed);
        await _groupService.Received(1).RemoveMemberAsync(1, 7, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(GroupRole.DungeonMaster)]
    [InlineData(GroupRole.Player)]
    public async Task TryRemoveMemberAsync_WhenTargetIsNotAnAdmin_RemovesThemWithoutCountingAdmins(GroupRole current)
    {
        GivenBoard(1, 7, current, admins: 1);

        var result = await _sut.TryRemoveMemberAsync(1, actingUserId: 99, targetUserId: 7, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.Changed);
        await _groupService.Received(1).RemoveMemberAsync(1, 7, Arg.Any<CancellationToken>());
        await _groupService.DidNotReceive().CountMembersWithRoleAsync(Arg.Any<int>(), Arg.Any<GroupRole>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryRemoveMemberAsync_WhenTargetIsNotAMember_ReturnsNotAMemberAndRemovesNobody()
    {
        GivenBoard(1, 7, null, admins: 1);

        var result = await _sut.TryRemoveMemberAsync(1, actingUserId: 99, targetUserId: 7, TestContext.Current.CancellationToken);

        result.Should().Be(MemberChangeOutcome.NotAMember);
        await AssertNobodyRemovedAsync();
    }

    [Fact]
    public async Task CreateOrAddToGroupAsync_WhenEmailIsBrandNew_CreatesAccountAndReturnsNewAccountCreated()
    {
        // Arrange
        const string email = "new@example.com";
        const string name = "New Player";
        _identityService.GetIdByEmailAsync(email).Returns((int?)null, 42);
        _identityService.CreateUserAsync(email, name).Returns(IdentityResult.Success);

        // Act
        var result = await _sut.CreateOrAddToGroupAsync(email, name, groupId: 1, GroupRole.Player, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome.Should().Be(CreateOrAddToGroupOutcome.NewAccountCreated);
        result.UserId.Should().Be(42);
        await _repository.Received(1).SetGroupRoleAsync(42, 1, GroupRole.Player);
        await _groupService.DidNotReceive().AddMemberAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GroupRole>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrAddToGroupAsync_WhenReResolutionAfterCreateReturnsNull_ReturnsFailed()
    {
        // Arrange — the account was created successfully, but the follow-up lookup by
        // email (e.g. replica lag, or the account being deleted between the two calls)
        // fails to find it. This must return Failed, not throw.
        const string email = "vanished@example.com";
        const string name = "Ghost Player";
        _identityService.GetIdByEmailAsync(email).Returns((int?)null, (int?)null);
        _identityService.CreateUserAsync(email, name).Returns(IdentityResult.Success);

        // Act
        var result = await _sut.CreateOrAddToGroupAsync(email, name, groupId: 1, GroupRole.Player, TestContext.Current.CancellationToken);

        // Assert
        result.Outcome.Should().Be(CreateOrAddToGroupOutcome.Failed);
        result.UserId.Should().BeNull();
        result.Errors.Should().NotBeEmpty();
        await _repository.DidNotReceive().SetGroupRoleAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<GroupRole>());
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
        var result = await _sut.CreateOrAddToGroupAsync(email, "Submitted Name", groupId: 1, GroupRole.Player, TestContext.Current.CancellationToken);

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
        var result = await _sut.CreateOrAddToGroupAsync(email, "Submitted Name", groupId: 1, GroupRole.Player, TestContext.Current.CancellationToken);

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
        var result = await _sut.CreateOrAddToGroupAsync(email, "Submitted Name", groupId: 1, GroupRole.Player, TestContext.Current.CancellationToken);

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
        await _sut.CreateOrAddToGroupAsync(email, "Ignored Name", groupId: 1, GroupRole.Player, TestContext.Current.CancellationToken);

        // Assert: the existing account's Name is never touched via a create call
        await _identityService.DidNotReceive().CreateUserAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------------------------------------------------------------------------
    // GetAvailableUsersAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAvailableUsersAsync_DelegatesToRepositoryAndReturnsSameList()
    {
        // Arrange
        var expectedList = new List<User> { new() { Id = 1, Name = "Available Player" } };
        _repository.GetAvailableUsers(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedList);

        // Act
        var result = await _sut.GetAvailableUsersAsync(1, "term", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(expectedList);
        await _repository.Received(1).GetAvailableUsers(1, "term", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAvailableUsersAsync_WhenSearchIsNull_ForwardsNullToRepositoryUnchanged()
    {
        // Arrange
        var expectedList = new List<User>();
        _repository.GetAvailableUsers(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedList);

        // Act
        await _sut.GetAvailableUsersAsync(1, null, TestContext.Current.CancellationToken);

        // Assert: the service must not substitute empty-string or filter itself
        await _repository.Received(1).GetAvailableUsers(1, null, Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------------------
    // GetAccountReachAsync and the guarded ResetPasswordAsync
    // ---------------------------------------------------------------------------

    private static IReadOnlySet<int> Ids(params int[] ids) => new HashSet<int>(ids);

    [Fact]
    public async Task GetAccountReachAsync_ForABoardAdminAndATargetWhoAdministersAnotherBoard_ReturnsAdministersAnotherBoard()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), 7, Arg.Any<CancellationToken>())
            .Returns(Ids(20));

        var result = await _sut.GetAccountReachAsync(actor, 7, [20, 21], TestContext.Current.CancellationToken);

        result[20].Should().Be(AccountReach.AdministersAnotherBoard);
        result[21].Should().Be(AccountReach.Reachable, "an account that is Admin only on the active board stays reachable");
    }

    [Fact]
    public async Task GetAccountReachAsync_AsksTheRepositoryOnceForAnyNumberOfTargets()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Ids());

        await _sut.GetAccountReachAsync(actor, 7, [20, 21, 22, 23, 24], TestContext.Current.CancellationToken);

        await _repository.Received(1).GetUserIdsAdministeringOtherGroupsAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 5), 7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccountReachAsync_ForASuperAdminActor_ReachesEveryAccountWithoutAQuery()
    {
        var actor = MakeSuperAdminPrincipal();

        var result = await _sut.GetAccountReachAsync(actor, 7, [20, 21], TestContext.Current.CancellationToken);

        result.Values.Should().OnlyContain(r => r == AccountReach.Reachable);
        await _repository.DidNotReceive().GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccountReachAsync_ForTheActorsOwnAccount_IsReachableEvenWhenTheyAdministerAnotherBoard()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(20);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Ids(20));

        var result = await _sut.GetAccountReachAsync(actor, 7, [20], TestContext.Current.CancellationToken);

        result[20].Should().Be(AccountReach.Reachable);
    }

    [Fact]
    public async Task GetAccountReachAsync_WithNoTargets_ReturnsEmptyWithoutAQuery()
    {
        var result = await _sut.GetAccountReachAsync(MakeNonSuperAdminPrincipal(), 7, [], TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        await _repository.DidNotReceive().GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ForATargetOutOfReach_FailsAndNeverCallsTheIdentityReset()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), 7, Arg.Any<CancellationToken>())
            .Returns(Ids(20));

        var result = await _sut.ResetPasswordAsync(actor, 7, new User { Id = 20, Name = "Elsewhere" }, "NewPassword123!");

        result.Succeeded.Should().BeFalse();
        await _identityService.DidNotReceive().AdminResetPasswordAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<int>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ForAReachableTarget_CallsTheIdentityReset()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), 7, Arg.Any<CancellationToken>())
            .Returns(Ids());
        _identityService.AdminResetPasswordAsync(actor, 21, "NewPassword123!").Returns(IdentityResult.Success);

        var result = await _sut.ResetPasswordAsync(actor, 7, new User { Id = 21, Name = "Here" }, "NewPassword123!");

        result.Succeeded.Should().BeTrue();
        await _identityService.Received(1).AdminResetPasswordAsync(actor, 21, "NewPassword123!");
    }

    [Fact]
    public async Task GetAccountReachAsync_ForASuperAdminTarget_ReturnsSuperAdminAccount_EvenWithAPlayerMembershipOnly()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), 7, Arg.Any<CancellationToken>())
            .Returns(Ids());
        _identityService.GetSuperAdminIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Ids(20));

        var result = await _sut.GetAccountReachAsync(actor, 7, [20, 21], TestContext.Current.CancellationToken);

        result[20].Should().Be(AccountReach.SuperAdminAccount, "the Identity role decides, whatever membership rows the account has");
        result[21].Should().Be(AccountReach.Reachable);
    }

    [Fact]
    public async Task GetAccountReachAsync_ForATargetThatIsBothSuperAdminAndAnotherBoardsAdmin_ReturnsSuperAdminAccount()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), 7, Arg.Any<CancellationToken>())
            .Returns(Ids(20));
        _identityService.GetSuperAdminIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Ids(20));

        var result = await _sut.GetAccountReachAsync(actor, 7, [20], TestContext.Current.CancellationToken);

        result[20].Should().Be(AccountReach.SuperAdminAccount);
    }

    [Fact]
    public async Task GetAccountReachAsync_ReadsSuperAdminIdsOnceForAnyNumberOfTargets()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Ids());
        _identityService.GetSuperAdminIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Ids());

        await _sut.GetAccountReachAsync(actor, 7, [20, 21, 22, 23], TestContext.Current.CancellationToken);

        await _identityService.Received(1).GetSuperAdminIdsAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 4), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccountReachAsync_ForASuperAdminActor_DoesNotReadTheIdentityRoles()
    {
        await _sut.GetAccountReachAsync(MakeSuperAdminPrincipal(), 7, [20], TestContext.Current.CancellationToken);

        await _identityService.DidNotReceive().GetSuperAdminIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ForASuperAdminTarget_FailsAndNeverCallsTheIdentityReset()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), 7, Arg.Any<CancellationToken>())
            .Returns(Ids());
        _identityService.GetSuperAdminIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Ids(20));

        var result = await _sut.ResetPasswordAsync(actor, 7, new User { Id = 20, Name = "Platform" }, "NewPassword123!");

        result.Succeeded.Should().BeFalse();
        await _identityService.DidNotReceive().AdminResetPasswordAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<int>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ForASuperAdminActor_ResetsAnAdminOfAnotherBoard()
    {
        var actor = MakeSuperAdminPrincipal();
        _identityService.AdminResetPasswordAsync(actor, 20, "NewPassword123!").Returns(IdentityResult.Success);

        var result = await _sut.ResetPasswordAsync(actor, 7, new User { Id = 20, Name = "Elsewhere" }, "NewPassword123!");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccountReachAsync_ForAnOrdinaryMemberOfAnotherBoard_IsReachable()
    {
        var actor = MakeNonSuperAdminPrincipal();
        _identityService.GetUserIdAsync(actor).Returns(1);
        _repository.GetUserIdsAdministeringOtherGroupsAsync(Arg.Any<IReadOnlyCollection<int>>(), 7, Arg.Any<CancellationToken>())
            .Returns(Ids());
        _identityService.GetSuperAdminIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Ids());

        var result = await _sut.GetAccountReachAsync(actor, 7, [20], TestContext.Current.CancellationToken);

        result[20].Should().Be(AccountReach.Reachable, "only an Admin of another board or a platform administrator is out of reach");
    }
}
