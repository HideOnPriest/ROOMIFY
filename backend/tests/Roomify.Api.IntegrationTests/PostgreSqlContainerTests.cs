using Npgsql;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "Integration")]
[Trait("RequiresDocker", "true")]
public sealed class PostgreSqlContainerTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact(DisplayName = "Tymczasowy PostgreSQL odpowiada wartością 1 na SELECT 1")]
    public async Task SelectOne_ReturnsOne()
    {
        // ARRANGE — connection string zawiera dynamiczny port z Testcontainers.
        // await using zamknie połączenie także przy niepowodzeniu asercji.
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        // ACT — prawdziwy PostgreSQL wykonuje SQL, bez tabel i migracji biznesowych.
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync();

        // ASSERT — potwierdzamy odpowiedź silnika; fixture później usuwa kontener.
        Assert.Equal(1, Assert.IsType<int>(result));
    }
}
