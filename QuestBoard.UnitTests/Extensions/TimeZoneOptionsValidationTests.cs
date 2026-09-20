using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Models;

namespace QuestBoard.UnitTests.Extensions;

// Two groups, mirroring CalendarFeedOptionsValidationTests: the predicate itself, then proof
// the predicate is actually wired into application startup.
public class TimeZoneOptionsValidationTests
{
    // -------------------------------------------------------------------
    // Predicate
    // -------------------------------------------------------------------

    [Fact]
    public void IsValid_DefaultConstructedOptions_IsTrue()
    {
        var options = new TimeZoneOptions();

        options.IsValid().Should().BeTrue();
        options.BoardTimeZoneId.Should().Be("Europe/Amsterdam");
    }

    [Fact]
    public void IsValid_EmptyString_IsFalse()
    {
        new TimeZoneOptions { BoardTimeZoneId = "" }.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_Whitespace_IsFalse()
    {
        new TimeZoneOptions { BoardTimeZoneId = "   " }.IsValid().Should().BeFalse();
    }

    // The opposite of the fail-fast trap: a garbage-but-non-empty id is not the validator's job
    // to catch. Zone resolution -- and the decision to degrade rather than crash -- belongs to
    // BoardClock, not to IsValid().
    [Fact]
    public void IsValid_UnresolvableButNonEmptyId_IsTrue()
    {
        new TimeZoneOptions { BoardTimeZoneId = "Definitely/NotAZone" }.IsValid().Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // Wiring
    // -------------------------------------------------------------------

    private static IConfiguration BuildConfiguration(string? boardTimeZoneId)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TimeZoneOptions.SectionName}:BoardTimeZoneId"] = boardTimeZoneId
            })
            .Build();
    }

    [Fact]
    public void AddDomainServices_EmptyBoardTimeZoneId_ResolvingOptionsThrowsOptionsValidationException()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(string.Empty);
        services.AddSingleton(configuration);

        services.AddDomainServices(configuration);
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<TimeZoneOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddDomainServices_ValidBoardTimeZoneId_ResolvingOptionsSucceedsAndCarriesTheValue()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("America/New_York");
        services.AddSingleton(configuration);

        services.AddDomainServices(configuration);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<TimeZoneOptions>>().Value;

        options.BoardTimeZoneId.Should().Be("America/New_York");
    }

    // A garbage-but-non-empty id must resolve through IOptions without throwing -- proving the
    // fail-fast trap is not present at this layer, since zone resolution (and its fallback) is
    // BoardClock's job, exercised separately in BoardClockTests.
    [Fact]
    public void AddDomainServices_UnresolvableButNonEmptyBoardTimeZoneId_ResolvingOptionsDoesNotThrow()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("Definitely/NotAZone");
        services.AddSingleton(configuration);

        services.AddDomainServices(configuration);
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<TimeZoneOptions>>().Value;

        act.Should().NotThrow();
    }
}
