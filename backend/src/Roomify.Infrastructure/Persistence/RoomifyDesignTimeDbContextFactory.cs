using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Roomify.Infrastructure.Persistence;

/// <summary>Narzędzia migracji odczytują wyłącznie jawne środowisko/argumenty, bez .env i bez startu API.</summary>
public sealed class RoomifyDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RoomifyDbContext>
{
    public RoomifyDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
        var options = new DbContextOptionsBuilder<RoomifyDbContext>();
        PostgreSqlConfiguration.Configure(options, configuration);
        return new RoomifyDbContext(options.Options);
    }
}
