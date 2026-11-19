using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// A host whose per-recipient mail limiter never refuses, for test classes that add existing accounts to a board many
/// times but are not about the mail budget. That limiter is a process-wide singleton that never resets, while clearing
/// the in-memory database restarts its ids, so such a class would otherwise spend one person's budget across many
/// unrelated tests and see a mail skipped. The per-administrator Create User limiter is keyed and left as it is. The
/// budget itself is covered by classes on the ordinary host that never reuse an id.
/// </summary>
public class UnlimitedMailBudgetWebApplicationFactory : WebApplicationFactoryBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            var perRecipient = services
                .Where(d => d.ServiceType == typeof(PartitionedRateLimiter<int>) && !d.IsKeyedService)
                .ToList();
            foreach (var descriptor in perRecipient)
                services.Remove(descriptor);

            services.AddSingleton(PartitionedRateLimiter.Create<int, string>(_ => RateLimitPartition.GetNoLimiter(string.Empty)));
        });
    }
}
