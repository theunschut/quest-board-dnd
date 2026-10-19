using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.Jobs;

namespace QuestBoard.IntegrationTests.Tests;

public class BoardMoveMailEnqueueTests(MoveWebApplicationFactory factory) : IClassFixture<MoveWebApplicationFactory>
{
    [Fact]
    public void EnqueueQuestMovedEmail_RecordsOneJobWithGroupIdSecond()
    {
        factory.JobClient.Clear();
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQuestEmailDispatcher>();

        dispatcher.EnqueueQuestMovedEmail(
            questId: 11, groupId: 5, audience: QuestMovedAudience.Removed, recipientEmail: "player@example.com",
            questTitle: "The Tomb", dmName: "Theomund", boardName: "The Silver Tankard",
            sessionDate: new DateTime(2031, 6, 14, 19, 0, 0));

        var job = factory.JobClient.EnqueuedJobs.Should().ContainSingle().Subject;
        job.Type.Should().Be(typeof(QuestMovedEmailJob));
        job.Method.Name.Should().Be(nameof(QuestMovedEmailJob.ExecuteAsync));
        job.Args[0].Should().Be(11);
        job.Args[1].Should().Be(5);
        job.Args[2].Should().Be(QuestMovedAudience.Removed);
        job.Args[3].Should().Be("player@example.com");
    }

    [Fact]
    public void EnqueueWaitlistPromotedAfterMoveEmail_RecordsAfterMoveEntryPoint()
    {
        factory.JobClient.Clear();
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQuestEmailDispatcher>();

        dispatcher.EnqueueWaitlistPromotedAfterMoveEmail(
            questId: 11, groupId: 5, finalizedDate: new DateTime(2031, 6, 14, 19, 0, 0),
            recipientEmail: "player@example.com", playerName: "Arannis", questTitle: "The Tomb",
            dmName: "Theomund", questDescription: "Desc", challengeRating: 9,
            movedToBoardName: "The Silver Tankard");

        var job = factory.JobClient.EnqueuedJobs.Should().ContainSingle().Subject;
        job.Type.Should().Be(typeof(QuestWaitlistPromotedEmailJob));
        job.Method.Name.Should().Be(nameof(QuestWaitlistPromotedEmailJob.ExecuteAfterMoveAsync));
        job.Args[1].Should().Be(5);
        job.Args[9].Should().Be("The Silver Tankard");
    }
}
