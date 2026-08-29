using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace QuestBoard.Domain.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        // The system clock is registered here so the domain reads time through an injectable
        // seam rather than a static call.
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<EmailSettings>().BindConfiguration("EmailSettings");
        // A code default (runway 20, preview 10) keeps the feature working on a deployment with
        // no matching configuration section -- nothing has to change on a server environment
        // file for it to work.
        services.AddOptions<EventSeriesOptions>().BindConfiguration(EventSeriesOptions.SectionName);
        // Same code-default-plus-configuration shape as EventSeriesOptions above: a
        // deployment with no matching configuration section still works.
        services.AddOptions<EventsOverviewOptions>()
            .BindConfiguration(EventsOverviewOptions.SectionName)
            .Validate(o => o.IsValid(), "EventsOverview DefaultTake, MaxTake and PageIncrement must each be at least 1, and DefaultTake must not exceed MaxTake.")
            .ValidateOnStart();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPlayerSignupService, PlayerSignupService>();
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<IShopService, ShopService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IDungeonMasterProfileService, DungeonMasterProfileService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEventSignupService, EventSignupService>();
        services.AddScoped<IEventSeriesService, EventSeriesService>();
        services.AddScoped<IImageValidationService, ImageValidationService>();
        // Singleton, not Scoped like everything above: this service is stateless -- it only holds
        // an immutable pre-built Markdig pipeline and two immutable sanitizer instances -- so it is
        // safe to share across concurrent requests without per-request allocation.
        services.AddSingleton<IMarkdownService, MarkdownService>();

        return services;
    }
}
