using GameServer.Application.Services;
using GameServer.Infrastructure;
using GameServer.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddGameServerInfrastructure(builder.Configuration);
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var canConnect = dbContext.Database.CanConnect();
        logger.LogInformation("[GameServer] PostgreSQL 연결 확인 결과: {CanConnect}", canConnect);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[GameServer] PostgreSQL 연결 확인 중 예외 발생");
    }
}

app.MapControllers();

app.Run();
