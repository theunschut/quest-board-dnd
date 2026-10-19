using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Services;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// The shared integration harness runs the Testing environment, which registers a dispatcher that
/// discards quest mail, so a test could never see what a quest move would send. This factory
/// routes quest mail through the real Hangfire dispatcher into the capturing job client instead,
/// so the move's mail can be asserted from the recorded jobs.
/// </summary>
public class MoveWebApplicationFactory : WebApplicationFactoryBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Registered after the base callback so this registration wins when
        // IQuestEmailDispatcher resolves the last-registered descriptor.
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IQuestEmailDispatcher, HangfireQuestEmailDispatcher>();
        });
    }
}
