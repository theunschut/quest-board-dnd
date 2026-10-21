using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Services;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// The shared integration harness binds the board-type resolver to a settable stub, so the page
/// resolver built on top of it never meets the real one. This factory puts the production
/// board-type resolver back, which exercises the real registration chain and so catches a circular
/// registration around the page resolver that the stub would hide.
/// </summary>
public class ProductionResolverWebApplicationFactory : WebApplicationFactoryBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Registered after the base callback so this registration wins when
        // GetRequiredService<IBoardTypeResolver>() resolves the last-registered descriptor.
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IBoardTypeResolver, BoardTypeResolver>();
        });
    }
}
