using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "PersistenceMetadata")]
[Trait("RequiresDocker", "false")]
public sealed class PersistenceRegistrationTests
{
    [Fact]
    public void Di_UsesComposeDatabaseSectionAndEscapesPassword()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Host"] = "db",
            ["Database:Port"] = "5432",
            ["Database:Name"] = "roomify_configuration_test",
            ["Database:Username"] = "configuration_test",
            ["Database:Password"] = "test-only;contains=separators",
        }).Build();
        using var services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        var connection = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal("db", connection.Host);
        Assert.Equal(5432, connection.Port);
        Assert.Equal("roomify_configuration_test", connection.Database);
        Assert.Equal("configuration_test", connection.Username);
        Assert.Equal("test-only;contains=separators", connection.Password);
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<RoomifyDbContext>());
        using var otherScope = services.CreateScope();
        Assert.NotSame(context, otherScope.ServiceProvider.GetRequiredService<RoomifyDbContext>());
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void Di_PrefersNamedConnectionStringForLocalAndTestHosts()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Roomify"] = "Host=127.0.0.1;Port=1;Database=roomify_test;Username=metadata_test",
            ["Database:Host"] = "db",
        }).Build();
        using var services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        var connection = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal("127.0.0.1", connection.Host);
        Assert.Equal(1, connection.Port);
        Assert.Equal("roomify_test", connection.Database);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        IdentityModelAssertions.AssertExpectedEntityTypes(context.Model);
    }

    [Fact]
    public void Di_WithoutConnectionSettingsStillBuildsProviderModel()
    {
        using var services = new ServiceCollection().AddInfrastructure(new ConfigurationBuilder().Build()).BuildServiceProvider();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        Assert.Null(context.Database.GetConnectionString());
        IdentityModelAssertions.AssertExpectedEntityTypes(context.Model);
        Assert.IsType<NpgsqlConnection>(context.Database.GetDbConnection());
    }

    [Fact]
    public async Task Api_HealthAndDbContextWorkWithoutReachableDatabase()
    {
        using var factory = new UnavailableDatabaseFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        IdentityModelAssertions.AssertExpectedEntityTypes(context.Model);
        Assert.Equal(1, new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString()).Port);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    private sealed class UnavailableDatabaseFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["HTTPS_PORT"] = null,
                    ["ConnectionStrings:Roomify"] = "Host=127.0.0.1;Port=1;Database=roomify_test;Username=metadata_test;Timeout=1",
                }));
        }
    }
}
