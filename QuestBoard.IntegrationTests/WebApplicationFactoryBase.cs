using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Collections.Concurrent;

namespace QuestBoard.IntegrationTests;

public class WebApplicationFactoryBase : WebApplicationFactory<Program>
{
    public TestDatabase Database { get; }
    public MutableGroupContext TestGroupContext { get; } = new MutableGroupContext();
    public CapturingBackgroundJobClient JobClient { get; } = new CapturingBackgroundJobClient();

    public WebApplicationFactoryBase()
    {
        Database = new TestDatabase($"QuestBoardTest_{Guid.NewGuid():N}");
    }

    protected override void ConfigureClient(HttpClient client)
    {
        // Don't follow redirects automatically so tests can verify redirect responses
        client.Timeout = TimeSpan.FromSeconds(30);
        base.ConfigureClient(client);
    }

    public HttpClient CreateNonRedirectingClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove all Entity Framework related services
            var efServiceDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<QuestBoardContext>) ||
                d.ServiceType == typeof(QuestBoardContext) ||
                d.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in efServiceDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add DbContext that uses the same InMemory database name as TestDatabase
            // This ensures all DbContext instances (test setup and web app) share the same database
            services.AddDbContext<QuestBoardContext>(options =>
            {
                options.UseInMemoryDatabase(Database.DatabaseName);
                options.EnableSensitiveDataLogging();
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Hangfire is not registered in the Testing environment, but AdminController depends on
            // IBackgroundJobClient — register a capturing spy so tests can assert which email job
            // was enqueued (a no-op stub would discard the enqueued Job objects).
            services.AddSingleton<IBackgroundJobClient>(JobClient);
            services.AddSingleton<IActiveGroupContext>(TestGroupContext);
            services.AddSingleton<IBoardTypeResolver>(TestGroupContext);

            // Replace IAntiforgery with a decorator that validates everything but delegates token generation
            var antiforgeryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Microsoft.AspNetCore.Antiforgery.IAntiforgery));
            if (antiforgeryDescriptor != null)
            {
                services.Remove(antiforgeryDescriptor);
                services.Add(ServiceDescriptor.Describe(
                    typeof(Microsoft.AspNetCore.Antiforgery.IAntiforgery),
                    sp =>
                    {
                        var inner = ActivatorUtilities.CreateInstance(sp, antiforgeryDescriptor.ImplementationType!);
                        return new TestAntiforgeryDecorator((Microsoft.AspNetCore.Antiforgery.IAntiforgery)inner);
                    },
                    antiforgeryDescriptor.Lifetime));
            }

            // Add test authentication scheme and make it the default
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Identity.Application";
                options.DefaultForbidScheme = "Identity.Application";
            });
        });
    }

    public void ResetDatabase()
    {
        Database.Reset();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Database?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Hangfire client used in integration tests that records every enqueued Job so tests can
/// assert which email job (if any) was dispatched by a given action, instead of discarding it.
/// </summary>
public class CapturingBackgroundJobClient : IBackgroundJobClient
{
    public ConcurrentBag<Job> EnqueuedJobs { get; } = new();

    public string Create(Job job, IState state)
    {
        EnqueuedJobs.Add(job);
        return "test-job-id";
    }

    public bool ChangeState(string jobId, IState state, string? expectedStateName) => true;

    public void Clear() => EnqueuedJobs.Clear();
}

/// <summary>
/// Decorator for anti-forgery service that delegates token generation to the real service
/// but always succeeds validation (for integration tests)
/// </summary>
public class TestAntiforgeryDecorator : Microsoft.AspNetCore.Antiforgery.IAntiforgery
{
    private readonly Microsoft.AspNetCore.Antiforgery.IAntiforgery _inner;

    public TestAntiforgeryDecorator(Microsoft.AspNetCore.Antiforgery.IAntiforgery inner)
    {
        _inner = inner;
    }

    public Microsoft.AspNetCore.Antiforgery.AntiforgeryTokenSet GetAndStoreTokens(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // Use the real implementation to generate and store tokens
        return _inner.GetAndStoreTokens(httpContext);
    }

    public Microsoft.AspNetCore.Antiforgery.AntiforgeryTokenSet GetTokens(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // Use the real implementation to get tokens
        return _inner.GetTokens(httpContext);
    }

    public Task<bool> IsRequestValidAsync(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // Always return true for test requests (skip validation)
        return Task.FromResult(true);
    }

    public void SetCookieTokenAndHeader(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // Use the real implementation
        _inner.SetCookieTokenAndHeader(httpContext);
    }

    public Task ValidateRequestAsync(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // Always succeed for tests (skip validation)
        return Task.CompletedTask;
    }
}