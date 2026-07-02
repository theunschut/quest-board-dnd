using QuestBoard.Service.Services;

namespace QuestBoard.UnitTests.Services;

[Trait("Category", "EmailStats")]
public class ResendStatsAggregatorTests
{
    private static DateTime Cutoff => new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ResendEmailRecord Record(string lastEvent, DateTime? createdAt = null)
        => new("id-1", createdAt ?? Cutoff.AddDays(1), lastEvent);

    [Fact]
    public void Aggregate_SentEvent_IncrementsSentOnly()
    {
        var records = new[] { Record("sent") };
        var result = ResendStatsAggregator.Aggregate(records, Cutoff);
        result.Sent.Should().Be(1);
        result.Delivered.Should().Be(0);
        result.Bounced.Should().Be(0);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public void Aggregate_DeliveredOpenedClicked_AllCountAsDelivered()
    {
        var records = new[]
        {
            Record("delivered"),
            Record("opened"),
            Record("clicked"),
        };
        var result = ResendStatsAggregator.Aggregate(records, Cutoff);
        result.Delivered.Should().Be(3);
        result.Sent.Should().Be(0);
        result.Bounced.Should().Be(0);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public void Aggregate_BouncedEvent_IncrementsBouncedOnly()
    {
        var records = new[] { Record("bounced") };
        var result = ResendStatsAggregator.Aggregate(records, Cutoff);
        result.Bounced.Should().Be(1);
        result.Sent.Should().Be(0);
        result.Delivered.Should().Be(0);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public void Aggregate_FailedEvent_IncrementsFailedOnly()
    {
        var records = new[] { Record("failed") };
        var result = ResendStatsAggregator.Aggregate(records, Cutoff);
        result.Failed.Should().Be(1);
        result.Sent.Should().Be(0);
        result.Delivered.Should().Be(0);
        result.Bounced.Should().Be(0);
    }

    [Fact]
    public void Aggregate_UnknownEvents_AreIgnored()
    {
        var records = new[]
        {
            Record("delivery_delayed"),
            Record("complained"),
            Record("scheduled"),
        };
        var result = ResendStatsAggregator.Aggregate(records, Cutoff);
        result.Sent.Should().Be(0);
        result.Delivered.Should().Be(0);
        result.Bounced.Should().Be(0);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public void Aggregate_RecordOlderThanCutoff_IsExcluded()
    {
        var oldRecord = Record("delivered", Cutoff.AddDays(-1));
        var result = ResendStatsAggregator.Aggregate(new[] { oldRecord }, Cutoff);
        result.Delivered.Should().Be(0);
    }

    [Fact]
    public void Aggregate_RecordNewerThanOrEqualToCutoff_IsIncluded()
    {
        var atCutoff = Record("delivered", Cutoff);
        var afterCutoff = Record("delivered", Cutoff.AddDays(1));
        var result = ResendStatsAggregator.Aggregate(new[] { atCutoff, afterCutoff }, Cutoff);
        result.Delivered.Should().Be(2);
    }

    [Fact]
    public void Aggregate_EmptySequence_ReturnsAllZeroCounts()
    {
        var result = ResendStatsAggregator.Aggregate(Enumerable.Empty<ResendEmailRecord>(), Cutoff);
        result.Sent.Should().Be(0);
        result.Delivered.Should().Be(0);
        result.Bounced.Should().Be(0);
        result.Failed.Should().Be(0);
    }
}
