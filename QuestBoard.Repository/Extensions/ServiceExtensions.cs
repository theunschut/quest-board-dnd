using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QuestBoard.Repository.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddRepositoryServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Entity Framework with SQL Server
        // Integration tests will override the connection string via configuration
        services.AddDbContext<QuestBoardContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlayerSignupRepository, PlayerSignupRepository>();
        services.AddScoped<IQuestRepository, QuestRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IUserTransactionRepository, UserTransactionRepository>();
        services.AddScoped<ITradeItemRepository, TradeItemRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IDungeonMasterProfileRepository, DungeonMasterProfileRepository>();
        services.AddScoped<IReminderLogRepository, ReminderLogRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IPlatformSettingRepository, PlatformSettingRepository>();

        // Register IdentityService (wraps UserManager/SignInManager; depends on UserEntity)
        services.AddScoped<IIdentityService, IdentityService>();

        return services;
    }

    public static IServiceProvider ConfigureDatabase(this IServiceProvider services)
    {
        // Apply any pending migrations automatically
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        context.Database.Migrate();

        return services;
    }
}
