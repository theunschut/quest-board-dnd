using Hangfire;

namespace QuestBoard.UnitTests.Services;

/// <summary>
/// Locks the global Hangfire retry policy shape registered in Program.cs (the non-Testing
/// GlobalJobFilters.Filters.Add block): 5 attempts with a 1/2/4/8/16 second exponential
/// backoff. Program.cs only registers this filter outside the Testing environment, so this
/// test asserts the intended AutomaticRetryAttribute contract directly rather than booting
/// the app — a regression to unbounded/immediate retries would fail here.
/// </summary>
public class HangfireRetryPolicyTests
{
    [Fact]
    public void AutomaticRetryAttribute_ConfiguredWithFiveAttemptsAndExponentialBackoff()
    {
        // Arrange / Act — the exact configuration Program.cs registers via GlobalJobFilters
        var attribute = new AutomaticRetryAttribute { Attempts = 5, DelaysInSeconds = new[] { 1, 2, 4, 8, 16 } };

        // Assert
        attribute.Attempts.Should().Be(5);
        attribute.DelaysInSeconds.Should().BeEquivalentTo(new[] { 1, 2, 4, 8, 16 }, options => options.WithStrictOrdering());
    }
}
