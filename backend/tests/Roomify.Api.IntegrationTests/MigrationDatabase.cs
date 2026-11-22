using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;
using Roomify.Infrastructure.Identity;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

/// <summary>Każdy przypadek ma własną bazę w kontenerze fixture, nigdy połączenie z .env.</summary>
internal sealed class MigrationDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _name = "roomify_mig_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; }

    private MigrationDatabase(string fixtureConnectionString)
    {
        _adminConnectionString = fixtureConnectionString;
        ConnectionString = new NpgsqlConnectionStringBuilder(fixtureConnectionString)
        {
            Database = _name, Pooling = false, Timeout = 10, CommandTimeout = 10,
        }.ConnectionString;
    }

    internal static async Task<MigrationDatabase> CreateAsync(PostgreSqlFixture fixture, bool migrate = true)
    {
        var database = new MigrationDatabase(fixture.ConnectionString);
        await using var admin = new NpgsqlConnection(database._adminConnectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database._name}\"", admin);
        await create.ExecuteNonQueryAsync();
        try
        {
            if (migrate)
            {
                await using var context = database.CreateContext();
                await context.Database.MigrateAsync();
            }
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    internal RoomifyDbContext CreateContext() => new(new DbContextOptionsBuilder<RoomifyDbContext>()
        .UseNpgsql(ConnectionString).Options);

    internal async Task<int> ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync();
    }

    internal async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        // Nazwa jest prywatnie generowana tutaj, a połączenie administracyjne pochodzi tylko z fixture.
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\"", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

internal static class MigrationTestData
{
    internal static readonly DateTimeOffset Start = new(2030, 1, 15, 9, 0, 0, TimeSpan.Zero);
    internal static readonly TimeProvider Clock = new FixedClock();

    internal static async Task<(Room Room, ApplicationUser User)> SeedAsync(MigrationDatabase database)
    {
        var room = Room.Create("Sala " + Guid.NewGuid().ToString("N"), null, "Piętro 1", 12, Clock);
        var email = Guid.NewGuid().ToString("N") + "@example.invalid";
        var user = new ApplicationUser(Clock)
        {
            UserName = email, NormalizedUserName = email.ToUpperInvariant(),
            Email = email, NormalizedEmail = email.ToUpperInvariant(),
        };
        await using var context = database.CreateContext();
        context.AddRange(room, user);
        await context.SaveChangesAsync();
        return (room, user);
    }

    internal static Reservation Reservation(Guid roomId, Guid userId, DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        Domain.Reservations.Reservation.Create(roomId, userId, "Spotkanie testowe",
            new ReservationPeriod(start ?? Start, end ?? Start.AddHours(1)), Clock);

    internal static Task<int> InsertAsync(MigrationDatabase database, Guid roomId, Guid userId,
        DateTimeOffset start, DateTimeOffset end, string status = "active") => database.ExecuteAsync("""
        INSERT INTO reservations(id,room_id,user_id,title,start_at,end_at,status,created_at)
        VALUES(@id,@room,@user,'Test integralności',@start,@end,@status,@created)
        """, Parameters(roomId, userId, start, end, status));

    internal static NpgsqlParameter[] Parameters(Guid roomId, Guid userId, DateTimeOffset start, DateTimeOffset end, string status) =>
    [
        new("id", Guid.NewGuid()), new("room", roomId), new("user", userId), new("start", start),
        new("end", end), new("status", status), new("created", Clock.GetUtcNow()),
    ];

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
