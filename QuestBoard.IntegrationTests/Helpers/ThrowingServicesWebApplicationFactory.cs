using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// A host in which the chosen services throw from every method. Each stub records the request's trace
/// identifier through the HTTP context accessor before it throws, so a fact can compare what the error page
/// shows with the id the request actually carried. The exception message is a fixed sentinel and its type is
/// a type of this assembly, so a fact can prove neither reaches a page.
/// </summary>
public abstract class ThrowingServicesWebApplicationFactory(params Type[] throwingServices) : WebApplicationFactoryBase
{
    public const string FailureSentinel = "sentinel-failure-text-7f3a91c2";

    /// <summary>The trace identifier of every request that reached a throwing stub, oldest first.</summary>
    public ConcurrentQueue<string> SeenTraceIdentifiers { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            foreach (var serviceType in throwingServices)
            {
                services.AddScoped(serviceType, provider =>
                {
                    var stub = DispatchProxy.Create(serviceType, typeof(ThrowingProxy));
                    var proxy = (ThrowingProxy)stub;
                    proxy.Accessor = provider.GetRequiredService<IHttpContextAccessor>();
                    proxy.Seen = SeenTraceIdentifiers;
                    return stub;
                });
            }
        });
    }

    public class ThrowingProxy : DispatchProxy
    {
        public IHttpContextAccessor Accessor { get; set; } = null!;

        public ConcurrentQueue<string> Seen { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var traceIdentifier = Accessor.HttpContext?.TraceIdentifier;
            if (traceIdentifier != null)
                Seen.Enqueue(traceIdentifier);

            throw new SentinelFailureException(FailureSentinel);
        }
    }
}

/// <summary>The exception every throwing stub raises; its type name must never appear on a page.</summary>
public sealed class SentinelFailureException(string message) : InvalidOperationException(message);

/// <summary>Only the quest service throws: a quest page ends in an unhandled exception.</summary>
public class ThrowingQuestServiceFactory() : ThrowingServicesWebApplicationFactory(typeof(IQuestService));

/// <summary>The services the layout and the page lean on throw, and so does the quest service.</summary>
public class ThrowingLayoutHelpersFactory()
    : ThrowingServicesWebApplicationFactory(
        typeof(IUserService), typeof(IBoardPageResolver), typeof(IGroupService), typeof(IQuestService));

/// <summary>The quest service throws and the console logger is configured to include scopes, as the setup guide describes.</summary>
public class ThrowingQuestServiceWithConsoleScopesFactory : ThrowingServicesWebApplicationFactory
{
    public ThrowingQuestServiceWithConsoleScopesFactory() : base(typeof(IQuestService))
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Logging:Console:IncludeScopes", "true");
    }
}
