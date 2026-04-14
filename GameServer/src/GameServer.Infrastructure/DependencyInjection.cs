using GameServer.Application.Interfaces;
using GameServer.Infrastructure.Persistence;
using GameServer.Infrastructure.Repositories;
using GameServer.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameServerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseOptions = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(databaseOptions);

        services.AddSingleton(databaseOptions);

        var connectionString = databaseOptions.BuildConnectionString();

        services.AddDbContext<GameDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
