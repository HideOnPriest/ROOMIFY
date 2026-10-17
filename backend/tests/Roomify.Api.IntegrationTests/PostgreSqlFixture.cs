using Testcontainers.PostgreSql;

namespace Roomify.Api.IntegrationTests;

/// <summary>
/// Fixture zarządza odrębnym, tymczasowym PostgreSQL. Nie odczytuje .env,
/// nie montuje developerskich wolumenów i nie używa istniejącej bazy Roomify.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Kontener PostgreSQL nie został uruchomiony.");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:18.6-bookworm")
            .WithDatabase("roomify_tests")
            .WithUsername("roomify_tests")
            .WithPassword(Guid.NewGuid().ToString("N"))
            // Losowy port hosta eliminuje konflikt z developerskim PostgreSQL.
            .WithPortBinding(5432, true)
            .WithCleanUp(true)
            .Build();

        try
        {
            await _container.StartAsync();
        }
        catch
        {
            // Sprzątamy także częściowo uruchomiony kontener; błąd nie jest sukcesem.
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }
}
