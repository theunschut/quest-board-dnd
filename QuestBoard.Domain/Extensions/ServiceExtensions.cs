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
        services.AddScoped<IShopSeedService, ShopSeedService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IDungeonMasterProfileService, DungeonMasterProfileService>();
        services.AddScoped<IGroupService, GroupService>();

        return services;
    }
}
