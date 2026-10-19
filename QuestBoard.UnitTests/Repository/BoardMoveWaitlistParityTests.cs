using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// The move picks promoted players in memory, because the quest has left its source board by the
// time a filtered repository read could run. This pins that selection to the repository's own
// top-candidate rule applied one freed seat at a time.
public class BoardMoveWaitlistParityTests
{
    private static readonly DateTime Finalized = new(2031, 3, 10, 19, 0, 0);
    private static readonly DateTime Base = new(2031, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    private sealed class FixedGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => 1;
    }

    [Fact]
    public async Task SelectPromotions_MatchesRepeatedTopWaitlistedCandidate()
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance).CreateMapper();

        await using var context = new QuestBoardContext(options, new FixedGroupContext());
        context.Groups.Add(new GroupEntity { Id = 1, Name = "Alpha" });
        for (var id = 1; id <= 12; id++)
        {
            context.UserEntities.Add(new UserEntity { Id = id, Name = $"User {id}", Email = $"u{id}@test.com", UserName = $"u{id}" });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var quest = new QuestEntity
        {
            Title = "Parity", Description = "d", GroupId = 1, DungeonMasterId = 1,
            IsFinalized = true, FinalizedDate = Finalized,
            ProposedDates = [new ProposedDateEntity { Date = Finalized }]
        };
        context.Quests.Add(quest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var pdId = quest.ProposedDates.Single().Id;

        PlayerSignupEntity Signup(int playerId, VoteType? vote, DateTime signedUp, DateTime? voteChanged = null,
            SignupRole role = SignupRole.Player, bool selected = false) => new()
        {
            QuestId = quest.Id, PlayerId = playerId, SignupRole = (int)role, IsSelected = selected,
            SignupTime = signedUp, LastVoteChangeTime = voteChanged,
            DateVotes = vote == null ? [] : [new PlayerDateVoteEntity { ProposedDateId = pdId, Vote = (int)vote }]
        };

        context.PlayerSignups.AddRange(
            Signup(2, VoteType.Yes, Base.AddHours(1)),
            Signup(3, VoteType.Yes, Base.AddHours(9)),
            // Signed up early but changed its vote late, so it ranks behind the later signup.
            Signup(4, VoteType.Yes, Base, voteChanged: Base.AddHours(20)),
            Signup(5, VoteType.Maybe, Base.AddHours(2)),
            Signup(6, VoteType.Maybe, Base.AddHours(3), voteChanged: Base.AddHours(1)),
            Signup(7, VoteType.No, Base),
            Signup(8, null, Base),
            Signup(9, VoteType.Yes, Base, role: SignupRole.AssistantDM),
            Signup(10, VoteType.Yes, Base, role: SignupRole.Spectator),
            Signup(11, VoteType.Yes, Base, selected: true));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The repository rule, one seat at a time.
        var repository = new PlayerSignupRepository(context, mapper);
        var repositorySequence = new List<int>();
        while (await repository.GetTopWaitlistedCandidateAsync(quest.Id, pdId, TestContext.Current.CancellationToken) is { } top)
        {
            repositorySequence.Add(top.Id);
            var entity = await context.PlayerSignups.SingleAsync(ps => ps.Id == top.Id, TestContext.Current.CancellationToken);
            entity.IsSelected = true;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The same signups as they stood before any promotion, selected through the in-memory rule.
        var fresh = new QuestBoardContext(options, new FixedGroupContext());
        var snapshot = await fresh.PlayerSignups.AsNoTracking().Include(ps => ps.DateVotes).Include(ps => ps.Player)
            .Where(ps => ps.QuestId == quest.Id).ToListAsync(TestContext.Current.CancellationToken);
        foreach (var entity in snapshot.Where(e => repositorySequence.Contains(e.Id)))
        {
            entity.IsSelected = false;
        }

        var domain = mapper.Map<List<PlayerSignup>>(snapshot);
        var inMemorySequence = domain.SelectPromotions(pdId, int.MaxValue).Select(s => s.Id).ToList();

        repositorySequence.Should().HaveCount(5);
        inMemorySequence.Should().Equal(repositorySequence);
    }

    [Fact]
    public void SelectPromotions_WithNoSeats_ReturnsNobody()
    {
        var signup = new PlayerSignup
        {
            Id = 1,
            Player = new QuestBoard.Domain.Models.User { Id = 1, Name = "A" },
            Quest = new Quest { Id = 1 },
            DateVotes = [new PlayerDateVote { ProposedDateId = 5, Vote = VoteType.Yes }]
        };

        new[] { signup }.SelectPromotions(5, 0).Should().BeEmpty();
        new[] { signup }.SelectPromotions(5, -1).Should().BeEmpty();
    }
}
