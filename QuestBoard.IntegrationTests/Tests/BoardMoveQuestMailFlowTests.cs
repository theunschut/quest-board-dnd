using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.Jobs;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The mail a quest move enqueues, read from the recorded jobs. Uses the same seeded quest as the
/// people facts: a finalized quest on board 1 moving to board 2, with a Dungeon Master, two kept
/// members, two removed people and the waitlisted member who is promoted.
///
/// What these facts do NOT establish: that a job renders or delivers. The jobs are recorded, not run.
/// </summary>
public class BoardMoveQuestMailFlowTests(MoveWebApplicationFactory factory)
    : IClassFixture<MoveWebApplicationFactory>, IAsyncLifetime
{
    private const int BoardBravo = 2;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private static IEnumerable<Job> MovedJobs(IEnumerable<Job> jobs, QuestMovedAudience audience) =>
        jobs.Where(j => j.Type == typeof(QuestMovedEmailJob) && Equals(j.Args[2], audience));

    [Fact]
    public async Task Move_EnqueuesOneMailPerAudienceMemberUnderTheTargetBoard()
    {
        var scenario = await BoardMoveQuestPeopleTests.SeedScenarioAsync(factory, "flow_audience");
        factory.JobClient.Clear();

        var response = await BoardMoveQuestPeopleTests.PostMoveAsync(factory, scenario);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var jobs = factory.JobClient.EnqueuedJobs.ToList();

        var promoted = jobs.Where(j => j.Type == typeof(QuestWaitlistPromotedEmailJob)).Should().ContainSingle().Subject;
        promoted.Method.Name.Should().Be(nameof(QuestWaitlistPromotedEmailJob.ExecuteAfterMoveAsync));
        promoted.Args[1].Should().Be(BoardBravo);
        promoted.Args[3].Should().Be(scenario.K2.Email);

        MovedJobs(jobs, QuestMovedAudience.DungeonMaster).Should().ContainSingle()
            .Which.Args[3].Should().Be(scenario.Dm.Email);
        MovedJobs(jobs, QuestMovedAudience.Player).Should().ContainSingle()
            .Which.Args[3].Should().Be(scenario.K1.Email);
        MovedJobs(jobs, QuestMovedAudience.Removed).Select(j => j.Args[3])
            .Should().BeEquivalentTo([scenario.D1.Email, scenario.D2.Email]);

        jobs.Where(j => j.Type == typeof(QuestMovedEmailJob)).Select(j => j.Args[1])
            .Should().OnlyContain(groupId => Equals(groupId, BoardBravo));
        jobs.Should().HaveCount(5);
    }

    [Fact]
    public async Task Move_SendsNothingToAnUnconfirmedMemberOrToTheActingSuperAdmin()
    {
        var scenario = await BoardMoveQuestPeopleTests.SeedScenarioAsync(factory, "flow_gates");
        var unconfirmed = await BoardMoveSeed.CreateUserAsync(factory, "flow_gates_unc", "Unconfirmed Member", emailConfirmed: false);
        await BoardMoveSeed.SeedMembershipAsync(factory, unconfirmed.Id, BoardBravo, GroupRole.Player);
        await BoardMoveSeed.SeedSignupAsync(
            factory, scenario.QuestId, unconfirmed.Id, SignupRole.Player, false, null,
            new Dictionary<int, VoteType>(), BoardMoveQuestPeopleTests.SignedUpAt);

        await BoardMoveSeed.SeedMembershipAsync(factory, scenario.Actor.Id, BoardBravo, GroupRole.Player);
        await BoardMoveSeed.SeedSignupAsync(
            factory, scenario.QuestId, scenario.Actor.Id, SignupRole.Player, false, null,
            new Dictionary<int, VoteType>(), BoardMoveQuestPeopleTests.SignedUpAt);
        factory.JobClient.Clear();

        var response = await BoardMoveQuestPeopleTests.PostMoveAsync(factory, scenario);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await BoardMoveSeed.ReadQuestAsync(factory, scenario.QuestId)).GroupId.Should().Be(BoardBravo);
        var recipients = factory.JobClient.EnqueuedJobs.Select(j => j.Args[3]).ToList();
        recipients.Should().NotContain(unconfirmed.Email);
        recipients.Should().NotContain(scenario.Actor.Email);
        recipients.Should().HaveCount(5);
    }

    [Fact]
    public async Task Refusal_EnqueuesNoMail()
    {
        var scenario = await BoardMoveQuestPeopleTests.SeedScenarioAsync(
            factory, "flow_refused", dmRoleOnBravo: GroupRole.Player);
        factory.JobClient.Clear();

        var response = await BoardMoveQuestPeopleTests.PostMoveAsync(factory, scenario, stamp: "anything");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        factory.JobClient.EnqueuedJobs.Should().BeEmpty();
    }
}
