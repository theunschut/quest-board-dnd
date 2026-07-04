using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.UnitTests.Extensions;

public class WaitlistOrderingTests
{
    private const int FinalizedProposedDateId = 5;

    private static PlayerSignup MakeSignup(int id, VoteType? vote, DateTime signupTime, DateTime? lastVoteChangeTime = null)
    {
        var signup = new PlayerSignup
        {
            Id = id,
            Player = new User { Id = id + 100, Name = $"Player {id}", Email = $"player{id}@test.com" },
            Quest = new Quest { Id = 1, Title = "T", Description = "D" },
            SignupTime = signupTime,
            LastVoteChangeTime = lastVoteChangeTime,
            IsSelected = false
        };

        if (vote != null)
        {
            signup.DateVotes = [new PlayerDateVote { ProposedDateId = FinalizedProposedDateId, Vote = vote }];
        }

        return signup;
    }

    [Fact]
    public void OrderWaitlist_YesMaybeNo_SortsYesFirstThenMaybeThenNo_RegardlessOfInputOrder()
    {
        // Arrange — deliberately scrambled input order
        var noVoter = MakeSignup(1, VoteType.No, DateTime.UtcNow.AddHours(-1));
        var yesVoter = MakeSignup(2, VoteType.Yes, DateTime.UtcNow.AddHours(-1));
        var maybeVoter = MakeSignup(3, VoteType.Maybe, DateTime.UtcNow.AddHours(-1));
        var waitlist = new List<PlayerSignup> { noVoter, maybeVoter, yesVoter };

        // Act
        var ordered = waitlist.OrderWaitlist(FinalizedProposedDateId).ToList();

        // Assert: VOTE-02 — Yes > Maybe > No
        ordered.Select(ps => ps.Id).Should().Equal(2, 3, 1);
    }

    [Fact]
    public void OrderWaitlist_TwoYesVoters_SortsByLastVoteChangeTimeAscending()
    {
        // Arrange: both voted Yes; the one who changed their vote earlier should come first
        var changedEarly = MakeSignup(1, VoteType.Yes, DateTime.UtcNow.AddDays(-3), lastVoteChangeTime: DateTime.UtcNow.AddHours(-2));
        var changedLate = MakeSignup(2, VoteType.Yes, DateTime.UtcNow.AddDays(-3), lastVoteChangeTime: DateTime.UtcNow.AddHours(-1));
        var waitlist = new List<PlayerSignup> { changedLate, changedEarly };

        // Act
        var ordered = waitlist.OrderWaitlist(FinalizedProposedDateId).ToList();

        // Assert: VOTE-02 tiebreak + VOTE-03 — earlier LastVoteChangeTime sorts first
        ordered.Select(ps => ps.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void OrderWaitlist_NullLastVoteChangeTime_FallsBackToSignupTime_InterleavesCorrectly()
    {
        // Arrange: signup A never changed its vote (LastVoteChangeTime == null), signed up
        // in the middle chronologically. Signup B changed vote recently. Signup C never
        // changed its vote and signed up earliest.
        var neverChangedMiddle = MakeSignup(1, VoteType.Yes, signupTime: DateTime.UtcNow.AddDays(-2), lastVoteChangeTime: null);
        var changedRecently = MakeSignup(2, VoteType.Yes, signupTime: DateTime.UtcNow.AddDays(-10), lastVoteChangeTime: DateTime.UtcNow.AddMinutes(-5));
        var neverChangedEarliest = MakeSignup(3, VoteType.Yes, signupTime: DateTime.UtcNow.AddDays(-5), lastVoteChangeTime: null);

        var waitlist = new List<PlayerSignup> { changedRecently, neverChangedMiddle, neverChangedEarliest };

        // Act
        var ordered = waitlist.OrderWaitlist(FinalizedProposedDateId).ToList();

        // Assert: effective ordering key is (LastVoteChangeTime ?? SignupTime) ascending:
        // neverChangedEarliest (-5 days) < neverChangedMiddle (-2 days) < changedRecently (-5 min)
        ordered.Select(ps => ps.Id).Should().Equal(3, 1, 2);
    }

    [Fact]
    public void OrderWaitlist_SignupWithNoVoteRecordForFinalizedDate_TreatedAsNo()
    {
        // Arrange: a signup with no DateVotes entry at all for the finalized date should sort
        // as if it voted No (lowest priority), not throw or sort as highest priority.
        var noVoteRecord = MakeSignup(1, vote: null, signupTime: DateTime.UtcNow.AddHours(-1));
        var yesVoter = MakeSignup(2, VoteType.Yes, signupTime: DateTime.UtcNow.AddHours(-1));

        var waitlist = new List<PlayerSignup> { noVoteRecord, yesVoter };

        // Act
        var ordered = waitlist.OrderWaitlist(FinalizedProposedDateId).ToList();

        // Assert
        ordered.Select(ps => ps.Id).Should().Equal(2, 1);
    }
}
