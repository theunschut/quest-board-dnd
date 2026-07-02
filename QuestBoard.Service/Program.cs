using QuestBoard.Repository.Automapper;
using QuestBoard.Service.Extensions;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository.Entities;
using QuestBoard.Repository.Extensions;
using QuestBoard.Service.Authorization;
using QuestBoard.Service.Automapper;
using QuestBoard.Service.Middleware;
using QuestBoard.Service.Services;
using QuestBoard.Service.ViewExpanders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.SqlServer;
using QuestBoard.Service.Jobs;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel server limits
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB (slightly higher than validation to allow for form overhead)
});


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationExpanders.Add(new MobileViewLocationExpander());
});

// Add health checks
builder.Services.AddHealthChecks();

// Add Identity using existing QuestBoardContext
builder.Services.AddIdentity<UserEntity, IdentityRole<int>>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Lockout settings
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<QuestBoardContext>()
.AddDefaultTokenProviders();

// Extend the shared "Default" token provider lifespan to 7 days.
// This affects password-reset, email-confirmation, and change-email tokens uniformly —
// net-new configuration block (framework default was 1 day, previously unconfigured).
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromDays(7);
});

// Add Authorization policies
builder.Services.AddScoped<IAuthorizationHandler, DungeonMasterHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdminHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DungeonMasterOnly", policy =>
        policy.Requirements.Add(new DungeonMasterRequirement()))
    .AddPolicy("AdminOnly", policy =>
        policy.Requirements.Add(new AdminRequirement()))
    .AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));

// Trust X-Forwarded-For from the configured reverse proxy (e.g. Traefik)
// so RemoteIpAddress reflects the real client instead of the proxy — otherwise every request
// shares one partition key below. KnownProxies comes from ReverseProxy:KnownProxies config
// (empty by default; set via env var in production, see docs/server-setup.md).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

    var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    foreach (var proxy in knownProxies)
    {
        if (IPAddress.TryParse(proxy, out var ip))
            options.KnownProxies.Add(ip);
    }
});

// Rate limit the ForgotPassword POST action — 3 requests / 15 minutes per client IP.
// SetPassword gets its own independent "set-password" policy (same limits) rather than sharing
// "forgot-password"'s budget — a legitimate forgot-password + set-password flow by one user
// shouldn't eat into the same 3-request window twice, and their abuse surfaces are distinct
// (anonymous spam vs. token-guessing).
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("forgot-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("set-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.", cancellationToken);
    };
});

// Rate-limit the repeatable manual admin email-send buttons (SendConfirmationEmail,
// EditUser's email-change branch), partitioned per TARGET userId so no single recipient's inbox is
// spammed regardless of which admin triggers it. This is a singleton PartitionedRateLimiter<int>
// consumed via AttemptAcquire in AdminController — NOT an AddRateLimiter policy, because userId/Id
// are POST form fields (not route values) and the policy-factory path runs before MVC model binding
// (RESEARCH.md Pitfall 1). 3 requests / 1 hour per target user, key "email-resend:{userId}".
// CreateUser's one-shot automated welcome email is explicitly exempt.
builder.Services.AddSingleton(_ => PartitionedRateLimiter.Create<int, string>(userId =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"email-resend:{userId}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true
        })));

// Back ASP.NET Core Session with a SQL Server distributed cache so
// ActiveGroupId (and other session data) survives app restarts, instead of the in-memory
// default that is wiped on every deploy. Guarded like the Hangfire branch below: the Testing
// environment falls back to AddDistributedMemoryCache so dotnet test never writes session rows
// into the real dev SQL Server database.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.SchemaName = "dbo";
        options.TableName = "AspNetSessionState";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add repositories
builder.Services
    .AddRepositoryServices(builder.Configuration)
    .AddDomainServices(builder.Configuration);

// Named HttpClient for Resend API stats
// Authorization header is NOT set here — added per-request in ResendStatsClient.FetchAllRecordsAsync
builder.Services.AddHttpClient("Resend", client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddScoped<ResendStatsClient>();

// Email render service and job dispatcher (Service-layer registrations)
builder.Services.AddScoped<IEmailRenderService, RazorEmailRenderService>();

// IActiveGroupContext — registered as Scoped; same scope as QuestBoardContext.
// In Testing environment the WebApplicationFactoryBase overrides this with MutableGroupContext singleton.
builder.Services.AddHttpContextAccessor();

// Dual registration pattern (see STATE.md):
//   1. AddScoped<ActiveGroupContextService>() — registers the CONCRETE type so that Hangfire
//      jobs (QuestFinalizedEmailJob, SessionReminderJob) can resolve it by concrete type and
//      call SetGroupId(groupId), which is NOT on the IActiveGroupContext interface.
//   2. AddScoped<IActiveGroupContext>(factory) — satisfies constructor-injected IActiveGroupContext
//      in controllers and domain services; the factory delegates to the SAME scoped instance,
//      so SetGroupId mutations are immediately visible to QuestBoardContext within the same scope.
// IMPORTANT: both registrations must stay in sync. Do NOT replace with AddScoped<IActiveGroupContext,
// ActiveGroupContextService>() alone — that breaks concrete-type resolution in the Hangfire jobs.
builder.Services.AddScoped<ActiveGroupContextService>();
builder.Services.AddScoped<IActiveGroupContext>(sp =>
    sp.GetRequiredService<ActiveGroupContextService>());

if (!builder.Environment.IsEnvironment("Testing"))
{
    // HangfireQuestEmailDispatcher requires IBackgroundJobClient which is only
    // registered when Hangfire is active (non-Testing environments).
    builder.Services.AddScoped<IQuestEmailDispatcher, HangfireQuestEmailDispatcher>();
    builder.Services.AddScoped<IReminderJobDispatcher, HangfireReminderJobDispatcher>();

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

    // Exponential backoff for transient job failures — 5 attempts over 1/2/4/8/16 seconds,
    // so transient SMTP/Resend/DB blips recover instead of exhausting Hangfire's default retries.
    GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 5, DelaysInSeconds = new[] { 1, 2, 4, 8, 16 } });

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 2;
    });
}
else
{
    // In the Testing environment Hangfire is skipped, so use a no-op dispatcher.
    builder.Services.AddScoped<IQuestEmailDispatcher, NullQuestEmailDispatcher>();
    builder.Services.AddScoped<IReminderJobDispatcher, NullReminderJobDispatcher>();
}

// Add automapper
builder.Services.AddAutoMapper(config =>
{
    config.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    config.AddProfile<ViewModelProfile>();
    config.AddProfile<EntityProfile>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.Configuration.DumpConfiguration();

// Configure the HTTP request pipeline.
// Must run first so RemoteIpAddress is corrected before any downstream middleware reads it.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseMiddleware<MobileDetectionMiddleware>();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseMiddleware<GroupSessionMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/hangfire"))
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.Redirect("/Account/Login");
                return;
            }

            if (!context.User.IsInRole("Admin") && !context.User.IsInRole("SuperAdmin"))
            {
                context.Response.Redirect("/Account/Login");
                return;
            }
        }

        await next();
    });

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new AdminDashboardAuthFilter() }
    });
}

app.MapAreaControllerRoute(
    name: "platform",
    areaName: "Platform",
    pattern: "platform/{controller=Group}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health");

// Only run migrations if not in testing environment
if (!app.Environment.IsEnvironment("Testing"))
{
    app.Services.ConfigureDatabase();

    // Seed basic shop data
    await SeedShopDataAsync(app);

    // Register daily session reminder sweep — runs at 09:00 server local time (CET/CEST).
    // Placed after ConfigureDatabase to ensure migrations have run before the job can fire (RESEARCH.md Pitfall 4).
    RecurringJob.AddOrUpdate<DailyReminderJob>(
        "daily-session-reminders",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 9 * * *");
}

// Fail fast in Production if email delivery is unconfigured — without this, SmtpClient creation
// silently no-ops (see EmailService.CreateSmtpClient) and quest/reminder/password-reset emails
// are dropped with nothing but a log warning. Development and Testing are intentionally exempt
// so local dev and the integration-test factory can boot without email config.
if (app.Environment.IsProduction())
{
    var missingEmailKeys = new List<string>();
    if (string.IsNullOrWhiteSpace(app.Configuration["EmailSettings:FromEmail"]))
        missingEmailKeys.Add("EmailSettings:FromEmail");
    if (string.IsNullOrWhiteSpace(app.Configuration["EmailSettings:SmtpServer"]))
        missingEmailKeys.Add("EmailSettings:SmtpServer");

    if (missingEmailKeys.Count > 0)
    {
        throw new InvalidOperationException(
            $"Email delivery cannot function without the following configuration key(s): {string.Join(", ", missingEmailKeys)}. " +
            "Set them via appsettings.json or environment variable overrides before starting in Production.");
    }
}

app.Run();

static async Task SeedShopDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var shopSeedService = scope.ServiceProvider.GetRequiredService<QuestBoard.Domain.Interfaces.IShopSeedService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();

        // Find first admin/DM user to attribute seed data to
        var adminUser = await userManager.Users.FirstOrDefaultAsync();
        if (adminUser != null)
        {
            await shopSeedService.SeedBasicEquipmentAsync(adminUser.Id);
        }
    }
    catch (Exception ex)
    {
        // Log error but don't stop application startup
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error seeding shop data");
    }
}