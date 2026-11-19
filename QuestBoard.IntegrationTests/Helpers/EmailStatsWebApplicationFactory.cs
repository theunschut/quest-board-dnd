using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// A host whose Resend key is a stub value and whose named Resend client talks to a canned handler instead of the
/// network, so the real statistics client, aggregator and page render the statistics without any real key. The
/// stub key is set on this host only; the shared harness and the committed settings still show the not-configured page.
/// </summary>
public class EmailStatsWebApplicationFactory : WebApplicationFactoryBase
{
    public const string StubApiKey = "re_stub_key_for_tests_only";

    /// <summary>The canned answers the named Resend client receives.</summary>
    public CannedResendHandler Handler { get; } = new();

    /// <summary>Set before the host is built to put this address in the Resend base address setting.</summary>
    public string? ResendBaseUrlSetting { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("EmailSettings:ResendApiKey", StubApiKey);
        if (ResendBaseUrlSetting != null)
            builder.UseSetting("EmailSettings:ResendBaseUrl", ResendBaseUrlSetting);

        // Registered after the app's own, so this primary handler replaces the one that would reach the network.
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient("Resend").ConfigurePrimaryHttpMessageHandler(() => Handler);
        });
    }
}
