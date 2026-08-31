using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Models;

namespace QuestBoard.UnitTests.Extensions;

// Two groups: the predicate itself (six facts) and proof that the predicate is actually wired
// into application startup (two facts) -- a correct predicate that is never registered would
// leave a bad deployment configuration silently unvalidated.
public class EventsOverviewOptionsValidationTests
{
    // -------------------------------------------------------------------
    // Predicate
    // -------------------------------------------------------------------

    [Fact]
    public void IsValid_DefaultConstructedOptions_IsTrue()
    {
        var options = new EventsOverviewOptions();

        options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_CeilingZero_IsFalse()
    {
        var options = new EventsOverviewOptions { MaxTake = 0 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_CeilingNegative_IsFalse()
    {
        var options = new EventsOverviewOptions { MaxTake = -1 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_IncrementZero_IsFalse()
    {
        var options = new EventsOverviewOptions { PageIncrement = 0 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_DefaultTakeZero_IsFalse()
    {
        var options = new EventsOverviewOptions { DefaultTake = 0 };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_DefaultTakeExceedsCeiling_IsFalse()
    {
        var options = new EventsOverviewOptions { DefaultTake = 20, MaxTake = 10 };

        options.IsValid().Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // Wiring
    // -------------------------------------------------------------------

    private static IConfiguration BuildConfiguration(int maxTake)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{EventsOverviewOptions.SectionName}:MaxTake"] = maxTake.ToString()
            })
            .Build();
    }

    [Fact]
    public void AddDomainServices_InvalidCeiling_ResolvingOptionsThrowsOptionsValidationException()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(maxTake: 0);
        services.AddSingleton(configuration);

        services.AddDomainServices(configuration);
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<EventsOverviewOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddDomainServices_ValidCeiling_ResolvingOptionsSucceedsAndCarriesTheValue()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(maxTake: 50);
        services.AddSingleton(configuration);

        services.AddDomainServices(configuration);
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<EventsOverviewOptions>>().Value;

        options.MaxTake.Should().Be(50);
    }
}
