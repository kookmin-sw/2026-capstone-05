namespace GameServer.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database:Postgres";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "gameserver_db";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "postgres";
    public string? ConnectionString { get; set; }

    public string BuildConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return ConnectionString;
        }

        return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};";
    }
}
