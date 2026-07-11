using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QuestBoard.Domain.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailSettings>().BindConfiguration("EmailSettings");

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPlayerSignupService, PlayerSignupService>();
        services.AddScoped<IQuestService, QuestService>();
        services.AddScoped<IShopService, ShopService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IDungeonMasterProfileService, DungeonMasterProfileService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IPlatformSettingService, PlatformSettingService>();
        services.AddScoped<IImageValidationService, ImageValidationService>();
        // Singleton, not Scoped like everything above: this service is stateless -- it only holds
        // an immutable pre-built Markdig pipeline and two immutable sanitizer instances -- so it is
        // safe to share across concurrent requests without per-request allocation.
        services.AddSingleton<IMarkdownService, MarkdownService>();

        return services;
    }
}
