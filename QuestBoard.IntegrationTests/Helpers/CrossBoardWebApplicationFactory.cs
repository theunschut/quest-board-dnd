using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Services;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// The shared integration harness substitutes a settable in-memory board context for the real
/// one, so a board switch written into session is invisible to the query filters and every
/// end-to-end assertion about switching boards would pass vacuously against that stub. This
/// factory restores the real session-backed context so the session itself is what drives the
/// query filters, exactly as it does in production, so a test can actually observe a board
/// switch happening.
/// </summary>
public class CrossBoardWebApplicationFactory : WebApplicationFactoryBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Registered after the base callback so this registration wins when
        // GetRequiredService<IActiveGroupContext>() resolves the last-registered descriptor.
        // IBoardTypeResolver is deliberately left bound to the base class's MutableGroupContext —
        // board-type-driven navigation rendering is not part of what this harness exists to prove,
        // and swapping it in here would drag unrelated seeding into every test that uses it.
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ActiveGroupContextService>();
            services.AddScoped<IActiveGroupContext>(sp =>
                sp.GetRequiredService<ActiveGroupContextService>());
        });
    }

    /// <summary>
    /// WebApplicationFactory's default client handler chain carries a cookie container, so the
    /// session cookie and the TempData cookie both round-trip across requests made on one client
    /// instance. That is the second thing tests against this factory need — a fresh HttpClient
    /// per request would never see the session write a previous request made.
    /// </summary>
    public HttpClient CreateCookieClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}
