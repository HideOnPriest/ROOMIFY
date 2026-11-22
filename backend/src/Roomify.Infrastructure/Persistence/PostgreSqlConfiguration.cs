using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Roomify.Infrastructure.Persistence;

internal static class PostgreSqlConfiguration
{
    internal static void Configure(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Roomify");
        var database = configuration.GetSection("Database");
        if (string.IsNullOrWhiteSpace(connectionString) && database.Exists())
        {
            var builder = new NpgsqlConnectionStringBuilder();
            if (database["Host"] is { } host) builder.Host = host;
            if (database["Port"] is { } port) builder.Port = int.Parse(port, System.Globalization.CultureInfo.InvariantCulture);
            if (database["Name"] is { } name) builder.Database = name;
            if (database["Username"] is { } username) builder.Username = username;
            if (database["Password"] is { } password) builder.Password = password;
            connectionString = builder.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(connectionString)) options.UseNpgsql();
        else options.UseNpgsql(connectionString);
    }
}
