using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Roomify.Domain.Reservations;
using Xunit.Abstractions;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "DatabaseMigrations")]
[Trait("RequiresDocker", "true")]
public sealed class PostgreSqlMigrationTests(PostgreSqlFixture fixture, ITestOutputHelper output) : IClassFixture<PostgreSqlFixture>
{
    [Fact(DisplayName = "MIG-001: InitialCreate działa na pustej bazie")]
    public async Task MIG001_InitialCreateAppliesToEmptyDatabase()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture, migrate: false);
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_tables WHERE schemaname='public'"));
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync("20261018093445_InitialCreate");
        Assert.EndsWith("_InitialCreate", Assert.Single(await context.Database.GetAppliedMigrationsAsync()));
        await context.Database.MigrateAsync();
        Assert.Equal(new[] { "20261018093445_InitialCreate", "20261018104209_ReservationOptimisticConcurrency", "20261018111403_RoomOptimisticConcurrency" }, await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact(DisplayName = "MIG-002: Powstały wszystkie tabele biznesowe i Identity")]
    public async Task MIG002_CreatesExactlyExpectedTables()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var names = await database.ScalarAsync<string>("SELECT string_agg(tablename, ',' ORDER BY tablename) FROM pg_tables WHERE schemaname='public'");
        Assert.Equal("__EFMigrationsHistory,reservations,role_claims,roles,rooms,user_claims,user_logins,user_roles,user_tokens,users", names);
    }

    [Fact(DisplayName = "MIG-003: Klucze główne i obce mają oczekiwaną definicję")]
    public async Task MIG003_KeysExistAndHistoryForeignKeysRestrictDeletion()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        Assert.Equal(9L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE contype='p' AND conrelid::regclass::text <> '\"__EFMigrationsHistory\"' AND connamespace='public'::regnamespace"));
        Assert.Equal(8L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE contype='f' AND connamespace='public'::regnamespace"));
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE conrelid='reservations'::regclass AND contype='f' AND confdeltype='r'"));
    }

    [Fact(DisplayName = "MIG-004: Indeksy, CHECK, kolacja i exclusion są wdrożone")]
    public async Task MIG004_IndexesAndConstraintsExist()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        Assert.Equal(3L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_indexes WHERE tablename='reservations' AND indexname IN ('ix_reservations_period','ix_reservations_room_period','ix_reservations_user_start')"));
        Assert.Equal(6L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE contype='c' AND connamespace='public'::regnamespace"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_constraint WHERE conname='ex_reservations_active_period' AND contype='x'"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_extension WHERE extname='btree_gist'"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_collation WHERE collname='roomify_case_insensitive' AND NOT collisdeterministic"));
    }

    [Fact(DisplayName = "MIG-005: Ponowne Migrate jest bezpieczne")]
    public async Task MIG005_ReapplyingMigrationPreservesExistingData()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        Assert.Equal(new[] { "20261018093445_InitialCreate", "20261018104209_ReservationOptimisticConcurrency", "20261018111403_RoomOptimisticConcurrency" }, await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(data.Room.Id, (await context.Rooms.SingleAsync()).Id);
        Assert.Equal(data.User.Id, (await context.Users.SingleAsync()).Id);
    }

    [Theory(DisplayName = "MIG-006: Baza odrzuca zerowy/odwrócony przedział")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task MIG006_InvalidPeriodIsRejected(int minutes)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        var error = await Assert.ThrowsAsync<PostgresException>(() => MigrationTestData.InsertAsync(database, data.Room.Id,
            data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddMinutes(minutes)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_reservations_period", error.ConstraintName);
    }

    [Theory(DisplayName = "MIG-007: Aktywne nakładające się przedziały są odrzucane")]
    [InlineData(30, 90)]
    [InlineData(-30, 90)]
    [InlineData(15, 45)]
    [InlineData(0, 60)]
    public async Task MIG007_OverlappingActiveReservationsAreRejected(int from, int until)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        var error = await Assert.ThrowsAsync<PostgresException>(() => MigrationTestData.InsertAsync(database, data.Room.Id,
            data.User.Id, MigrationTestData.Start.AddMinutes(from), MigrationTestData.Start.AddMinutes(until)));
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, error.SqlState);
        Assert.Equal("ex_reservations_active_period", error.ConstraintName);
    }

    [Theory(DisplayName = "MIG-008: Granice stykające się są dozwolone")]
    [InlineData(-60, 0)]
    [InlineData(60, 120)]
    public async Task MIG008_AdjacentReservationsAreAllowed(int from, int until)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start.AddMinutes(from), MigrationTestData.Start.AddMinutes(until));
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
    }

    [Fact(DisplayName = "MIG-009: Anulowanie zwalnia termin, a ponowna aktywacja podlega exclusion")]
    public async Task MIG009_CancelledReservationFreesTerm()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        await database.ExecuteAsync("UPDATE reservations SET status='cancelled'");
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync("UPDATE reservations SET status='active' WHERE status='cancelled'"));
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, error.SqlState);
    }

    [Theory(DisplayName = "MIG-010: Równoczesne transakcje kończą się dokładnie jednym sukcesem")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public async Task MIG010_ConcurrentTransactionsCannotDoubleBook(int iteration)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;
        async Task<string> Attempt(int offsetMinutes)
        {
            await using var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync(timeout.Token);
            await using var transaction = await connection.BeginTransactionAsync(timeout.Token);
            if (Interlocked.Increment(ref participants) == 2) ready.SetResult();
            await ready.Task.WaitAsync(timeout.Token);
            try
            {
                await using var command = new NpgsqlCommand("""
                    INSERT INTO reservations(id,room_id,user_id,title,start_at,end_at,status,created_at)
                    VALUES(@id,@room,@user,'Współbieżność',@start,@end,@status,@created)
                    """, connection, transaction);
                command.Parameters.AddRange(MigrationTestData.Parameters(data.Room.Id, data.User.Id,
                    MigrationTestData.Start.AddMinutes(offsetMinutes), MigrationTestData.Start.AddHours(1).AddMinutes(offsetMinutes), "active"));
                await command.ExecuteNonQueryAsync(timeout.Token);
                await transaction.CommitAsync(timeout.Token);
                return "committed";
            }
            catch (PostgresException error)
            {
                await transaction.RollbackAsync(timeout.Token);
                return error.SqlState;
            }
        }
        var offsetMinutes = iteration % 3 * 15;
        var results = await Task.WhenAll(Attempt(0), Attempt(offsetMinutes)).WaitAsync(timeout.Token);
        output.WriteLine("Próba {0}, przesunięcie {1} min: transakcja A={2}, B={3}", iteration, offsetMinutes, results[0], results[1]);
        Assert.Single(results, result => result == "committed");
        var rejected = Assert.Single(results, result => result != "committed");
        // Równoczesne sprawdzanie GiST może utworzyć cykl blokad. PostgreSQL wtedy
        // przerywa jedną transakcję kodem 40P01; nadal nie dopuszcza podwójnego zapisu.
        // MIG-011 osobno wymaga dokładnie 23P01 dla naruszenia exclusion constraint.
        Assert.Contains(rejected, new[] { PostgresErrorCodes.ExclusionViolation, PostgresErrorCodes.DeadlockDetected });
        var persisted = await database.ScalarAsync<long>("SELECT count(*) FROM reservations WHERE status='active'");
        output.WriteLine("Utrwalone aktywne rezerwacje: {0}", persisted);
        Assert.Equal(1L, persisted);
    }

    [Fact(DisplayName = "MIG-011: Npgsql i EF udostępniają kod 23P01 i nazwę ograniczenia")]
    public async Task MIG011_EfWrapsExclusionViolationWithoutLosingSqlState()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await using (var first = database.CreateContext())
        {
            first.Add(MigrationTestData.Reservation(data.Room.Id, data.User.Id));
            await first.SaveChangesAsync();
        }
        await using var second = database.CreateContext();
        second.Add(MigrationTestData.Reservation(data.Room.Id, data.User.Id));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal("23P01", postgres.SqlState);
        Assert.Equal("ex_reservations_active_period", postgres.ConstraintName);
    }

    [Theory(DisplayName = "MIG-012/013: Rezerwacja wymaga istniejącej sali i użytkownika")]
    [InlineData(true, "fk_reservations_rooms_room_id")]
    [InlineData(false, "fk_reservations_users_user_id")]
    public async Task MIG012_013_MissingReferencesAreRejected(bool missingRoom, string constraint)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        var error = await Assert.ThrowsAsync<PostgresException>(() => MigrationTestData.InsertAsync(database,
            missingRoom ? Guid.NewGuid() : data.Room.Id, missingRoom ? data.User.Id : Guid.NewGuid(),
            MigrationTestData.Start, MigrationTestData.Start.AddHours(1)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Theory(DisplayName = "MIG-014: Usunięcie użytkownika lub sali nie kasuje rezerwacji")]
    [InlineData("users", "fk_reservations_users_user_id")]
    [InlineData("rooms", "fk_reservations_rooms_room_id")]
    public async Task MIG014_ReferencedEntitiesCannotDeleteHistory(string table, string constraint)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync($"DELETE FROM {table}"));
        Assert.Equal(constraint, error.ConstraintName);
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
        Assert.Equal(1L, await database.ScalarAsync<long>($"SELECT count(*) FROM {table}"));
    }

    [Fact(DisplayName = "MIG-015: Dane domenowe i Identity przechodzą rzeczywisty round-trip UTC")]
    public async Task MIG015_DataRoundTripsThroughPostgreSql()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        var reservation = MigrationTestData.Reservation(data.Room.Id, data.User.Id);
        reservation.Cancel(MigrationTestData.Clock);
        await using (var context = database.CreateContext())
        {
            context.Add(reservation);
            await context.SaveChangesAsync();
        }
        await using var read = database.CreateContext();
        var result = await read.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(reservation.Id, result.Id);
        Assert.Equal(reservation.Period, result.Period);
        Assert.Equal(ReservationStatus.Cancelled, result.Status);
        Assert.Equal(TimeSpan.Zero, result.StartAt.Offset);
        Assert.Equal(TimeSpan.Zero, result.EndAt.Offset);
        Assert.Equal(reservation.CreatedAt, result.CreatedAt);
        Assert.Equal(reservation.UpdatedAt, result.UpdatedAt);
        Assert.Equal(data.User.Id, (await read.Users.SingleAsync()).Id);
        Assert.Equal(data.Room.Name, (await read.Rooms.SingleAsync()).Name);
    }

    [Fact(DisplayName = "Rollback: Down usuwa tylko schemat jednorazowej bazy, następne Up działa")]
    public async Task Down_RemovesOwnedSchemaAndAllowsReapplication()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        await MigrationTestData.SeedAsync(database);
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_tables WHERE schemaname='public'"));
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_collation WHERE collname='roomify_case_insensitive'"));
        // Rozszerzenie może być zasobem współdzielonym; Down nie usuwa go kaskadowo.
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM pg_extension WHERE extname='btree_gist'"));
        await context.Database.MigrateAsync();
        Assert.Equal(0, await context.Users.CountAsync());
    }

    [Fact]
    public async Task DifferentRooms_CanUseIdenticalActivePeriod()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var first = await MigrationTestData.SeedAsync(database);
        var other = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, first.Room.Id, first.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        await MigrationTestData.InsertAsync(database, other.Room.Id, other.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
    }

    [Theory]
    [InlineData("UPDATE rooms SET capacity=0", "ck_rooms_capacity_positive")]
    [InlineData("UPDATE rooms SET name=' '", "ck_rooms_name_not_blank")]
    [InlineData("UPDATE rooms SET location=' '", "ck_rooms_location_not_blank")]
    public async Task RoomChecks_RejectInvalidValues(string sql, string constraint)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        await MigrationTestData.SeedAsync(database);
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(sql));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task StatusAndCaseInsensitiveUniqueness_AreEnforced()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var first = await MigrationTestData.SeedAsync(database);
        var second = await MigrationTestData.SeedAsync(database);
        var invalidStatus = await Assert.ThrowsAsync<PostgresException>(() => MigrationTestData.InsertAsync(database,
            first.Room.Id, first.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1), "pending"));
        Assert.Equal("ck_reservations_status", invalidStatus.ConstraintName);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync("UPDATE rooms SET name=@name WHERE id=@id",
            new NpgsqlParameter("name", first.Room.Name.ToUpperInvariant()), new NpgsqlParameter("id", second.Room.Id)));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        Assert.Equal("ux_rooms_name", duplicate.ConstraintName);
    }
}
