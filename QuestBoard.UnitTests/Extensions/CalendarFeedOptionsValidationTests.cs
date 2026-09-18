using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Models;

namespace QuestBoard.UnitTests.Extensions;

// Two groups: the predicate itself and proof that the predicate is actually wired into
// application startup -- a correct predicate that is never registered would leave a bad
// deployment configuration silently unvalidated.
public class CalendarFeedOptionsValidationTests
{
    // -------------------------------------------------------------------
    // Predicate
    // -------------------------------------------------------------------

    [Fact]
    public void IsValid_DefaultConstructedOptions_IsTrue()
    {
        var options = new CalendarFeedOptions();

        options.IsValid().Should().BeTrue();
        options.MonthsBack.Should().Be(3);
        options.MonthsAhead.Should().Be(12);
        options.LastFetchedThrottleMinutes.Should().Be(15);
    }

    [Fact]
    public void IsValid_MonthsBackZero_IsTrue()
    {
        var options = new CalendarFeedOptions { MonthsBack = 0 };

        options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_MonthsBackNegative_IsFalse()
    {
        var options = new CalendarFeedOptions { MonthsBack = -1 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_MonthsAheadZero_IsFalse()
    {
        var options = new CalendarFeedOptions { MonthsAhead = 0 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_MonthsAheadNegative_IsFalse()
    {
        var options = new CalendarFeedOptions { MonthsAhead = -1 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_ThrottleMinutesZero_IsFalse()
    {
        var options = new CalendarFeedOptions { LastFetchedThrottleMinutes = 0 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_ThrottleMinutesNegative_IsFalse()
    {
        var options = new CalendarFeedOptions { LastFetchedThrottleMinutes = -1 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_RetentionDaysZero_IsFalse()
    {
        var options = new CalendarFeedOptions { RetentionDays = 0 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_RetentionDaysNegative_IsFalse()
    {
        var options = new CalendarFeedOptions { RetentionDays = -1 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_EveryBoundaryValue_IsTrue()
    {
        var options = new CalendarFeedOptions
        {
            MonthsBack = 0,
            MonthsAhead = 1,
            LastFetchedThrottleMinutes = 1,
            RetentionDays = 1
        };

        options.IsValid().Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // Wiring
    // -------------------------------------------------------------------

    private static IConfiguration BuildConfiguration(int monthsAhead)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{CalendarFeedOptions.SectionName}:MonthsAhead"] = monthsAhead.ToString()
            })
            .Build();
    }

    [Fact]
    public void AddDomainServices_InvalidMonthsAhead_ResolvingOptionsThrowsOptionsValidationException()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(monthsAhead: 0);
        services.AddSingleton(configuration);

        services.AddDomainServices(configuration);
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<CalendarFeedOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddDomainServices_ValidMonthsAhead_ResolvingOptionsSucceedsAndCarriesTheValue()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(monthsAhead: 6);
        services.AddSingleton(configuration);

        services.AddDomainServices(configuration);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CalendarFeedOptions>>().Value;

        options.MonthsAhead.Should().Be(6);
    }
}
