using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roomify.Domain.Reservations;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Identity;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "DatabaseAudit")]
[Trait("RequiresDocker", "true")]
public sealed class PostgreSqlSchemaAuditTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task PeriodUpdate_DoesNotConflictWithItselfAndRejectedChangePreservesStoredPeriod()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id,
            MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        await using var context = database.CreateContext();
        var reservation = await context.Reservations.SingleAsync();
        var original = reservation.Period;
        var originalVersion = context.Entry(reservation).Property<uint>("xmin").OriginalValue;
        // Jawny UPDATE obu kolumn sprawdza wykluczenie własnego rekordu (EDGE-013).
        Assert.Equal(1, await database.ExecuteAsync("UPDATE reservations SET start_at=@start, end_at=@end WHERE id=@id",
            new NpgsqlParameter("start", original.Start), new NpgsqlParameter("end", original.End),
            new NpgsqlParameter("id", reservation.Id)));
        // Nawet UPDATE bez zmiany wartości tworzy nową wersję wiersza PostgreSQL.
        // Ten scenariusz bada exclusion, więc odczytujemy nową wersję przed dalszą edycją.
        await context.Entry(reservation).ReloadAsync();
        Assert.NotEqual(originalVersion, context.Entry(reservation).Property<uint>("xmin").OriginalValue);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id,
            original.Start.AddHours(2), original.End.AddHours(2));
        reservation.ChangePeriod(new ReservationPeriod(original.Start.AddMinutes(90), original.End.AddMinutes(90)), MigrationTestData.Clock);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgres.SqlState);
        Assert.Equal("ex_reservations_active_period", postgres.ConstraintName);
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.AsNoTracking().SingleAsync(x => x.Id == reservation.Id);
        Assert.Equal(original, persisted.Period);
        Assert.Null(persisted.UpdatedAt);
        Assert.Equal(2, await read.Reservations.CountAsync());
    }

    [Fact]
    public async Task AppliedMigration_ExactlyMatchesModelColumnsAndPrimaryKeys()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        await using var context = database.CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var expectedColumns = model.Tables.SelectMany(table => table.Columns.Where(column => column.Name != "xmin").Select(column =>
            $"{table.Name}.{column.Name}:{column.StoreType}:{column.IsNullable}")).OrderBy(x => x).ToArray();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT c.relname, a.attname, format_type(a.atttypid, a.atttypmod), NOT a.attnotnull
            FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid
            JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='public' AND c.relkind='r' AND c.relname <> '__EFMigrationsHistory'
                AND a.attnum > 0 AND NOT a.attisdropped
            """, connection);
        var actualColumns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                actualColumns.Add($"{reader.GetString(0)}.{reader.GetString(1)}:{reader.GetString(2)}:{reader.GetBoolean(3)}");
        }
        Assert.Equal(expectedColumns, actualColumns.OrderBy(x => x));
        // xmin jest systemowym atrybutem PostgreSQL, nie nową fizyczną kolumną użytkownika.
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_attribute WHERE attrelid IN ('reservations'::regclass, 'rooms'::regclass) AND attname='xmin' AND attnum < 0 AND atttypid='xid'::regtype"));

        var expectedKeys = model.Tables.Select(table =>
            $"{table.Name}:{string.Join(',', table.PrimaryKey!.Columns.Select(column => column.Name))}").OrderBy(x => x);
        command.CommandText = """
            SELECT t.relname, string_agg(a.attname, ',' ORDER BY k.position)
            FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
            CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY AS k(number, position)
            JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=k.number
            WHERE c.connamespace='public'::regnamespace AND c.contype='p'
                AND t.relname <> '__EFMigrationsHistory' GROUP BY t.relname
            """;
        var actualKeys = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) actualKeys.Add($"{reader.GetString(0)}:{reader.GetString(1)}");
        }
        Assert.Equal(expectedKeys, actualKeys.OrderBy(x => x));
        Assert.False(context.Database.HasPendingModelChanges());
        var exclusion = await database.ScalarAsync<string>("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='ex_reservations_active_period'");
        Assert.Contains("EXCLUDE USING gist", exclusion);
        Assert.Contains("room_id WITH =", exclusion);
        Assert.Contains("tstzrange(start_at, end_at, '[)'::text) WITH &&", exclusion);
        Assert.Contains("status", exclusion);
        Assert.Contains("'active'", exclusion);
    }

    [Fact]
    public async Task IdentityStores_NormalizePersistAndRejectDuplicateEmailOnRealPostgreSql()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Roomify"] = database.ConnectionString,
        }).Build();
        await using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = "Audyt." + Guid.NewGuid().ToString("N") + "@example.invalid";
        var user = new ApplicationUser(MigrationTestData.Clock) { Email = email, UserName = email };
        // Hasło jest wyłącznie technicznym wejściem testu w usuwanej bazie Testcontainers.
        var created = await manager.CreateAsync(user, "Testcontainers-Haslo-123");
        Assert.True(created.Succeeded, string.Join(';', created.Errors.Select(error => error.Description)));
        await using var read = database.CreateContext();
        var persisted = await read.Users.AsNoTracking().SingleAsync();
        Assert.Equal(email.ToUpperInvariant(), persisted.NormalizedEmail);
        Assert.Equal(email.ToUpperInvariant(), persisted.NormalizedUserName);
        Assert.NotEqual("Testcontainers-Haslo-123", persisted.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, manager.PasswordHasher.VerifyHashedPassword(
            persisted, persisted.PasswordHash!, "Testcontainers-Haslo-123"));

        // Omijamy walidator managera, by niezależnie sprawdzić fizyczny indeks bazy.
        var duplicate = new ApplicationUser(MigrationTestData.Clock)
        {
            Email = email.ToLowerInvariant(), NormalizedEmail = persisted.NormalizedEmail,
            UserName = "other", NormalizedUserName = "OTHER",
        };
        read.Users.Add(duplicate);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => read.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_users_normalized_email", postgres.ConstraintName);
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM users"));
    }
}
