using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class QuestUpdateSignupCharacterTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();

    // IAsyncLifetime — reset the singleton group context after each test class run so that
    // state mutated by any test in this class does not bleed into subsequently-executed
    // test classes.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WithDifferentActiveCharacter_UpdatesSignupCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm1", "charswapdm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 1");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap1", "charswap1@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "First Character");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Second Character");

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: firstCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(secondCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WithNoCharacterIdField_ClearsSignupCharacterIdToNull()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm2", "charswapdm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 2");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap2", "charswap2@example.com");

        var character = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id);

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: character.Id);

        // A disabled <select> posts no field for that name at all, so the body carries only
        // questId — omit the characterId key entirely rather than sending an empty string.
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_OnFinalizedQuest_UpdatesSignupCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm3", "charswapdm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Swap Quest 3", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap3", "charswap3@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "First Character");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Second Character");

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: firstCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert — no finalization cutoff applies to this action
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(secondCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_ForWaitlistedSignup_UpdatesSignupCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm4", "charswapdm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 4");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap4", "charswap4@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "First Character");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Second Character");

        await TestDataHelper.CreatePlayerSignupAsync(
            factory.Services, quest.Id, player.Id, isSelected: false, characterId: firstCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert — waitlisted status is unrelated to whether the character swap is allowed
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(secondCharacter.Id);
    }

    [Theory]
    [InlineData(0)] // Player
    [InlineData(1)] // Spectator
    [InlineData(2)] // AssistantDM
    public async Task UpdateSignupCharacter_Post_ForEachSignupRole_UpdatesSignupCharacterId(int signupRole)
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"charswapdm5r{signupRole}", $"charswapdm5r{signupRole}@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, $"Swap Quest 5 Role {signupRole}");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"charswap5r{signupRole}", $"charswap5r{signupRole}@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "First Character");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Second Character");

        await TestDataHelper.CreatePlayerSignupAsync(
            factory.Services, quest.Id, player.Id, signupRole: signupRole, characterId: firstCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(secondCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WithAnotherUsersCharacterInSameBoard_ReturnsBadRequestAndLeavesCharacterUnchanged()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm6", "charswapdm6@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 6");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap6", "charswap6@example.com");
        var ownCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Own Character");

        var otherUser = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswap6other", "charswap6other@example.com");
        var otherCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, otherUser.Id, "Other Player's Character");

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: ownCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = otherCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(ownCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WithRetiredCharacter_AssignsIt()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm7", "charswapdm7@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 7");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap7", "charswap7@example.com");

        var activeCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Active Character", status: 0);
        var retiredCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Retired Character", status: 1);

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: activeCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = retiredCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert — a Retired character owned by the caller is a valid selection
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(retiredCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WithDeadCharacter_AssignsIt()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm8", "charswapdm8@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 8");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap8", "charswap8@example.com");

        var activeCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Active Character", status: 0);
        var deadCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Dead Character", status: 2);

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: activeCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = deadCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert — a Dead character owned by the caller is a valid selection
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(deadCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_ResubmittingTheCurrentRetiredCharacter_LeavesItAssigned()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm9", "charswapdm9@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 9");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap9", "charswap9@example.com");

        var retiredCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Retired Character", status: 1);

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: retiredCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = retiredCharacter.Id.ToString()
        });

        // Act — this is the persistence half of the no-op-save guarantee; the rendered
        // pre-selected option is covered by view-level work in a later plan.
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(retiredCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WhenCallerHasNoSignupOnTheQuest_RedirectsToDetailsWithoutTouchingAnySignup()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm10", "charswapdm10@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 10");

        var otherPlayer = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswap10other", "charswap10other@example.com");
        var otherCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, otherPlayer.Id, "Other Player's Character");
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, otherPlayer.Id, characterId: otherCharacter.Id);

        var (callerClient, caller) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap10", "charswap10@example.com");
        var callerCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, caller.Id, "Caller's Character");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = callerCharacter.Id.ToString()
        });

        // Act — caller has no signup row on this quest at all
        var response = await callerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert — a friendly redirect back to Details, not a raw 400 body
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(quest.Id.ToString());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var otherSignup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == otherPlayer.Id, TestContext.Current.CancellationToken);
        otherSignup.Should().NotBeNull();
        otherSignup!.CharacterId.Should().Be(otherCharacter.Id);
    }

    /// <summary>
    /// Proves the board boundary holds for this action end to end: a character seeded under a
    /// different board than the caller's active one is rejected. It cannot distinguish which of
    /// the two layers rejected the request, because the entity's model-level board filter
    /// resolves the foreign character to null before the action's own comparison is ever
    /// reached. The same-board, different-owner test above is the isolatable ownership case —
    /// this one only proves the boundary holds, not which check inside it fired.
    /// </summary>
    [Fact]
    public async Task UpdateSignupCharacter_Post_WithCharacterFromAnotherBoard_ReturnsBadRequestAndLeavesCharacterUnchanged()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm11", "charswapdm11@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 11");

        await using (var seedContext = factory.Database.CreateContext()) // ActiveGroupId = null (sees all for seeding)
        {
            seedContext.Groups.Add(new GroupEntity { Id = 2, Name = "Other Board", CreatedAt = DateTime.UtcNow });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap11", "charswap11@example.com");

        var ownCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Own Board Character", groupId: 1);
        var otherBoardCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Other Board Character", groupId: 2);

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: ownCharacter.Id);

        factory.TestGroupContext.ActiveGroupId = 1;

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = otherBoardCharacter.Id.ToString()
        });

        try
        {
            // Act
            var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var signup = await context.PlayerSignups
                .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
            signup.Should().NotBeNull();
            signup!.CharacterId.Should().Be(ownCharacter.Id);
        }
        finally
        {
            // Restore hygiene in case this method mutated the singleton mid-test, so sibling
            // tests in this class are unaffected by execution order.
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }
    // A character change is a scalar edit, but the repository update rewrites the signup's
    // whole date-vote collection from the model it is handed. If the signup is loaded without
    // its votes, saving a new character silently deletes them — which drops the player from
    // reminder eligibility and waitlist promotion while telling them the change succeeded.
    [Fact]
    public async Task UpdateSignupCharacter_Post_WhenSignupHasDateVotes_LeavesThoseVotesIntact()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charvotedm", "charvotedm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Vote Preserving Quest");
        var proposedDate = await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.UtcNow.AddDays(5));

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charvote", "charvote@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Vote First");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Vote Second");

        var signup = await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: firstCharacter.Id);

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            seedContext.Set<PlayerDateVoteEntity>().Add(
                new PlayerDateVoteEntity { PlayerSignupId = signup.Id, ProposedDateId = proposedDate.Id, Vote = (int)VoteType.Yes });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var savedSignup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        savedSignup.Should().NotBeNull();
        savedSignup!.CharacterId.Should().Be(secondCharacter.Id);

        var votes = await context.Set<PlayerDateVoteEntity>()
            .Where(v => v.PlayerSignupId == signup.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        votes.Should().ContainSingle("changing a character must not disturb the player's date votes");
        votes[0].ProposedDateId.Should().Be(proposedDate.Id);
        votes[0].Vote.Should().Be((int)VoteType.Yes);
    }
}
