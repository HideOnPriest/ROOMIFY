using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roomify.Application;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Application.Rooms;
using Roomify.Domain.Rooms;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Persistence;
using Roomify.Infrastructure.Persistence.Repositories;

namespace Roomify.Api.IntegrationTests;

[Trait("RequiresDocker", "true")]
[Trait("Category", "RoomManagement")]
public sealed class RoomManagementTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task INT_ROOM_001_002_CreateAndRead_RoundTripUsesReadDtoAndNoTracking()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        await using var services = Services(database);
        await using var scope = services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<CreateRoomService>().CreateAsync(new("  Wisła  ", "  Opis ", " Piętro 1 ", 12));
        Assert.Equal(RoomOperationStatus.Success, created.Status);
        Assert.True(created.Value!.IsActive); Assert.Equal("Wisła", created.Value.Name);
        Assert.Equal("Opis", created.Value.Description); Assert.Equal("Piętro 1", created.Value.Location);
        Assert.Equal(MigrationTestData.Clock.GetUtcNow(), created.Value.CreatedAt);
        await using var read = services.CreateAsyncScope();
        var fetched = await read.ServiceProvider.GetRequiredService<GetRoomByIdService>().GetAsync(created.Value.Id);
        Assert.Equal(created.Value, fetched.Value);
        Assert.Empty(read.ServiceProvider.GetRequiredService<RoomifyDbContext>().ChangeTracker.Entries());
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM rooms"));
    }

    [Fact]
    public async Task INT_ROOM_003_Update_PreservesIdentityCreationAndEveryReservation()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await SeedHistory(database);
        var history = await History(database);
        await using var services = Services(database);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<UpdateRoomService>().UpdateAsync(new(data.Id, "  Odra ", "Nowy opis", " Piętro 2 ", 1));
        Assert.Equal(RoomOperationStatus.Success, result.Status);
        Assert.Equal(data.Id, result.Value!.Id); Assert.Equal(data.CreatedAt, result.Value.CreatedAt);
        Assert.Equal("Odra", result.Value.Name); Assert.Equal("Piętro 2", result.Value.Location); Assert.Equal(1, result.Value.Capacity);
        Assert.Equal(history, await History(database));
        await using var read = database.CreateContext();
        Assert.Equal(result.Value, RoomDetails.From(await read.Rooms.SingleAsync()));
        // Ta sama nazwa nie jest konfliktem z własnym rekordem.
        Assert.Equal(RoomOperationStatus.Success, (await scope.ServiceProvider.GetRequiredService<UpdateRoomService>()
            .UpdateAsync(new(data.Id, "odra", null, "Piętro 3", 2))).Status);
    }

    [Fact]
    public async Task INT_ROOM_004_005_006_007_013_ArchivePreservesHistoryAndActivateAllowsOnlyNewBookings()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var room = await SeedHistory(database);
        var history = await History(database);
        await using var services = Services(database);
        await using (var scope = services.CreateAsyncScope())
        {
            var archive = scope.ServiceProvider.GetRequiredService<ArchiveRoomService>();
            Assert.Equal(RoomOperationStatus.Success, (await archive.ArchiveAsync(room.Id)).Status);
            Assert.Equal(RoomOperationStatus.AlreadyArchived, (await archive.ArchiveAsync(room.Id)).Status);
        }
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM rooms WHERE NOT is_active"));
        Assert.Equal(history, await History(database));
        var start = MigrationTestData.Start.AddDays(1);
        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(new(room.Id, "Nowe", start, start.AddHours(1)));
            Assert.Equal(ReservationOperationStatus.RoomInactive, result.Status); Assert.Null(result.Reservation);
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var activate = scope.ServiceProvider.GetRequiredService<ActivateRoomService>();
            Assert.Equal(RoomOperationStatus.Success, (await activate.ActivateAsync(room.Id)).Status);
            Assert.Equal(RoomOperationStatus.AlreadyActive, (await activate.ActivateAsync(room.Id)).Status);
        }
        Assert.Equal(history, await History(database));
        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(new(room.Id, "Nowe", start, start.AddHours(1)));
            Assert.Equal(ReservationOperationStatus.Success, result.Status);
        }
        Assert.Equal(4L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations WHERE status='cancelled'"));
        // Fizyczne usunięcie jest zablokowane przez FK RESTRICT, także po archiwizacji.
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync("DELETE FROM rooms"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        Assert.Equal("fk_reservations_rooms_room_id", error.ConstraintName);
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM rooms"));
        Assert.Equal(4L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
    }

    [Fact]
    public async Task INT_ROOM_008_FiltersAndProjectionExecuteInPostgreSql()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        await SeedRooms(database);
        var sql = new SqlObserver();
        await using var services = Services(database, observer: sql);
        await using var scope = services.CreateAsyncScope();
        var list = scope.ServiceProvider.GetRequiredService<GetRoomsService>();
        var result = await list.GetAsync(new(Search: "wisła", Location: "piętro", MinCapacity: 20, IsActive: true, PageSize: 1));
        Assert.Equal(RoomOperationStatus.Success, result.Status);
        Assert.Equal("Wisła duża", Assert.Single(result.Value!.Items).Name); Assert.Equal(1, result.Value.TotalCount);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<RoomifyDbContext>().ChangeTracker.Entries());
        Assert.Equal(2, sql.Commands.Count);
        var query = sql.Commands.Last();
        foreach (var part in new[] { "WHERE", "LIKE", "ILIKE", "capacity", "is_active", "ORDER BY", "LIMIT", "OFFSET" }) Assert.Contains(part, query);
        Assert.DoesNotContain("reservations", query); Assert.DoesNotContain("xmin", query);
        Assert.Empty((await list.GetAsync(new(Search: "Nieistniejąca"))).Value!.Items);
        Assert.Equal("Dosłowna %_\\", Assert.Single((await list.GetAsync(new(Search: "%_\\"))).Value!.Items).Name);
        Assert.Single((await list.GetAsync(new(IsActive: false))).Value!.Items);
    }

    [Theory]
    [InlineData(RoomSortBy.Name, RoomSortDirection.Ascending)] [InlineData(RoomSortBy.Name, RoomSortDirection.Descending)]
    [InlineData(RoomSortBy.Location, RoomSortDirection.Ascending)] [InlineData(RoomSortBy.Location, RoomSortDirection.Descending)]
    [InlineData(RoomSortBy.Capacity, RoomSortDirection.Ascending)] [InlineData(RoomSortBy.Capacity, RoomSortDirection.Descending)]
    [InlineData(RoomSortBy.CreatedAt, RoomSortDirection.Ascending)] [InlineData(RoomSortBy.CreatedAt, RoomSortDirection.Descending)]
    public async Task INT_ROOM_009_PagingIsStableWithTiesAndWhitelistedSort(RoomSortBy sort, RoomSortDirection direction)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture); await SeedRooms(database);
        var sql = new SqlObserver();
        await using var services = Services(database, observer: sql); await using var scope = services.CreateAsyncScope();
        var list = scope.ServiceProvider.GetRequiredService<GetRoomsService>();
        var all = (await list.GetAsync(new(SortBy: sort, SortDirection: direction))).Value!;
        var collected = new List<Guid>();
        for (var i = 1; i <= 3; i++)
        {
            var request = new GetRoomsRequest(i, 2, SortBy: sort, SortDirection: direction);
            var first = (await list.GetAsync(request)).Value!; var repeat = (await list.GetAsync(request)).Value!;
            Assert.Equal(all.TotalCount, first.TotalCount); Assert.Equal(i, first.PageNumber); Assert.Equal(2, first.PageSize);
            Assert.Equal(first.Items, repeat.Items); collected.AddRange(first.Items.Select(x => x.Id));
        }
        Assert.Equal(all.Items.Select(x => x.Id), collected); Assert.Equal(5, collected.Distinct().Count());
        var clause = $"r.{sort switch { RoomSortBy.Name => "name", RoomSortBy.Location => "location", RoomSortBy.Capacity => "capacity", _ => "created_at" }}";
        Assert.Contains("ORDER BY " + clause + (direction == RoomSortDirection.Descending ? " DESC" : "") + ", r.id", sql.Commands.Last());
        Assert.Empty((await list.GetAsync(new(99, 2))).Value!.Items);
    }

    [Fact]
    public async Task INT_ROOM_010_UniquePrecheckAndConcurrentDatabaseConstraintIncludeArchivedNames()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var gate = new AsyncGate();
        await using var services = Services(database, afterNameCheck: gate.WaitAsync);
        async Task<RoomOperationResult<RoomDetails>> Create(string name)
        {
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<CreateRoomService>().CreateAsync(new(name, null, "Piętro", 1));
        }
        var results = await Task.WhenAll(Create("Wisła"), Create("WISŁA"));
        Assert.Single(results, x => x.Status == RoomOperationStatus.Success);
        Assert.Single(results, x => x.Status == RoomOperationStatus.DuplicateName && x.Value is null);
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM rooms"));
        await using var ordinaryServices = Services(database); await using var scope = ordinaryServices.CreateAsyncScope();
        var room = results.Single(x => x.Value is not null).Value!;
        await scope.ServiceProvider.GetRequiredService<ArchiveRoomService>().ArchiveAsync(room.Id);
        Assert.Equal(RoomOperationStatus.DuplicateName, (await scope.ServiceProvider.GetRequiredService<CreateRoomService>().CreateAsync(new("wisła", null, "Piętro", 2))).Status);
        await using var other = database.CreateContext();
        Assert.Equal(RoomWriteOutcome.DuplicateName, await new EfRoomRepository(other).AddAsync(Room.Create("wisła", null, "Piętro", 1, MigrationTestData.Clock), default));
        Assert.Empty(other.ChangeTracker.Entries());
    }

    [Theory] [InlineData("update")] [InlineData("archive")]
    public async Task INT_ROOM_011_TwoAdministratorsCannotSilentlyOverwriteSameVersion(string secondOperation)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database); var gate = new AsyncGate(); var sql = new SqlObserver();
        await using var services = Services(database, observer: sql, afterRead: gate.WaitAsync);
        async Task<RoomOperationResult<RoomDetails>> Change(bool second)
        {
            await using var scope = services.CreateAsyncScope();
            if (second && secondOperation == "archive") return await scope.ServiceProvider.GetRequiredService<ArchiveRoomService>().ArchiveAsync(data.Room.Id);
            return await scope.ServiceProvider.GetRequiredService<UpdateRoomService>().UpdateAsync(new(data.Room.Id, second ? "B" : "A", null, "Piętro", second ? 22 : 11));
        }
        var results = await Task.WhenAll(Change(false), Change(true));
        var winner = Assert.Single(results, x => x.Status == RoomOperationStatus.Success);
        Assert.Single(results, x => x.Status == RoomOperationStatus.ConcurrentModification && x.ErrorCode == "room.concurrentModification" && x.Value is null);
        await using var verify = database.CreateContext(); Assert.Equal(winner.Value, RoomDetails.From(await verify.Rooms.SingleAsync()));
        Assert.Contains(sql.Commands, x => x.Contains("UPDATE rooms") && x.Contains("AND xmin ="));
    }

    [Theory]
    [InlineData("name", "'   '", "23514", "ck_rooms_name_not_blank")]
    [InlineData("location", "'   '", "23514", "ck_rooms_location_not_blank")]
    [InlineData("capacity", "0", "23514", "ck_rooms_capacity_positive")]
    [InlineData("name", "repeat('x',101)", "22001", null)]
    [InlineData("location", "repeat('x',151)", "22001", null)]
    public async Task INT_ROOM_012_PhysicalConstraintsRejectInvalidData(string column, string value, string state, string? constraint)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        await MigrationTestData.SeedAsync(database);
        var before = await database.ScalarAsync<string>("SELECT row_to_json(r)::text FROM rooms r");
        // Wyłącznie stałe InlineData tego testu, bez danych użytkownika i bez runtime nazwy kolumny.
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync($"UPDATE rooms SET {column}={value}"));
        Assert.Equal(state, error.SqlState); Assert.Equal(constraint, error.ConstraintName);
        Assert.Equal(before, await database.ScalarAsync<string>("SELECT row_to_json(r)::text FROM rooms r"));
    }

    [Fact]
    public async Task Update_DuplicateNamePrecheckAndDatabaseConstraintPreserveStoredRoom()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture); await SeedRooms(database);
        await using var context = database.CreateContext();
        var repository = new EfRoomRepository(context);
        var room = (await repository.FindForUpdateAsync(await context.Rooms.Where(x => x.Name == "Odra").Select(x => x.Id).SingleAsync(), default))!;
        var before = RoomDetails.From(room);
        await using var services = Services(database); await using var scope = services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UpdateRoomService>();
        var precheck = await service.UpdateAsync(new(room.Id, "WISŁA DUŻA", null, "Zmiana", 1));
        Assert.Equal(RoomOperationStatus.DuplicateName, precheck.Status); Assert.Null(precheck.Value);
        var invalid = await service.UpdateAsync(new(room.Id, "Nowa", null, "Zmiana", 0));
        Assert.Equal(RoomOperationStatus.InvalidInput, invalid.Status); Assert.Null(invalid.Value);
        // Symulacja konkurenta, którego precheck nie mógł wcześniej zauważyć.
        room.UpdateDetails("WISŁA DUŻA", null, "Zmiana", 1);
        Assert.Equal(RoomWriteOutcome.DuplicateName, await repository.SaveAsync(room, default));
        Assert.Empty(context.ChangeTracker.Entries());
        await using var verify = database.CreateContext();
        Assert.Equal(before, RoomDetails.From(await verify.Rooms.SingleAsync(x => x.Id == room.Id)));
    }

    [Fact]
    public async Task StorageClassification_DoesNotMisreportPrimaryKeyOrCheckAsDuplicateName()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture); var data = await MigrationTestData.SeedAsync(database);
        await using var duplicate = database.CreateContext(); var repository = new EfRoomRepository(duplicate);
        await Assert.ThrowsAsync<ReservationStorageException>(() => repository.AddAsync(data.Room, default));
        Assert.Empty(duplicate.ChangeTracker.Entries());
        await using var context = database.CreateContext(); var tracked = (await new EfRoomRepository(context).FindForUpdateAsync(data.Room.Id, default))!;
        context.Entry(tracked).Property(x => x.Capacity).CurrentValue = 0;
        await Assert.ThrowsAsync<ReservationStorageException>(() => new EfRoomRepository(context).SaveAsync(tracked, default));
        Assert.Empty(context.ChangeTracker.Entries());
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(data.Room, default));
    }

    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task TrustedIdentity_RealRepositoriesEnforceAdministrativeWritesAndReadScope(bool authenticated, bool administrator)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture); var room = await SeedHistory(database);
        await database.ExecuteAsync("UPDATE rooms SET is_active=false"); var history = await History(database);
        await using var services = Services(database, authenticated: authenticated, administrator: administrator);
        await using var scope = services.CreateAsyncScope(); var sp = scope.ServiceProvider;
        var expected = authenticated ? (administrator ? RoomOperationStatus.Success : RoomOperationStatus.Forbidden) : RoomOperationStatus.Unauthenticated;
        var write = await sp.GetRequiredService<CreateRoomService>().CreateAsync(new("Nowa", null, "Piętro", 1));
        Assert.Equal(expected, write.Status);
        if (!administrator)
        {
            Assert.Equal(expected, (await sp.GetRequiredService<UpdateRoomService>().UpdateAsync(new(room.Id, "Zmiana", null, "Piętro", 1))).Status);
            Assert.Equal(expected, (await sp.GetRequiredService<ArchiveRoomService>().ArchiveAsync(room.Id)).Status);
            Assert.Equal(expected, (await sp.GetRequiredService<ActivateRoomService>().ActivateAsync(room.Id)).Status);
        }
        var detail = await sp.GetRequiredService<GetRoomByIdService>().GetAsync(room.Id);
        Assert.Equal(!authenticated ? RoomOperationStatus.Unauthenticated : administrator ? RoomOperationStatus.Success : RoomOperationStatus.NotFound, detail.Status);
        var list = await sp.GetRequiredService<GetRoomsService>().GetAsync(new());
        if (!authenticated) Assert.Null(list.Value);
        else if (!administrator) { Assert.Empty(list.Value!.Items); Assert.Equal(RoomOperationStatus.Forbidden, (await sp.GetRequiredService<GetRoomsService>().GetAsync(new(IsActive: false))).Status); }
        else Assert.Contains(list.Value!.Items, x => x.Id == room.Id && !x.IsActive);
        Assert.Equal(history, await History(database));
    }

    [Fact]
    public async Task RoomXminMigration_UpgradeDowngradePreservesRowsAndNativeVersion()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture, migrate: false);
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync("20261018104209_ReservationOptimisticConcurrency");
        await SeedHistory(database); var history = await History(database);
        var before = await database.ScalarAsync<string>("SELECT row_to_json(r)::text FROM rooms r");
        var xmin = await database.ScalarAsync<uint>("SELECT xmin FROM rooms");
        await context.Database.MigrateAsync(); Assert.Equal(3, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(before, await database.ScalarAsync<string>("SELECT row_to_json(r)::text FROM rooms r"));
        Assert.Equal(xmin, await database.ScalarAsync<uint>("SELECT xmin FROM rooms"));
        await context.GetService<IMigrator>().MigrateAsync("20261018104209_ReservationOptimisticConcurrency");
        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(before, await database.ScalarAsync<string>("SELECT row_to_json(r)::text FROM rooms r"));
        Assert.Equal(xmin, await database.ScalarAsync<uint>("SELECT xmin FROM rooms")); Assert.Equal(history, await History(database));
        await context.Database.MigrateAsync(); Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task INT_ROOM_014_FixtureDatabaseHasIndependentIdentityAndNoDeveloperConnection()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString);
        Assert.StartsWith("roomify_mig_", connection.Database); Assert.Equal("roomify_tests", connection.Username);
        Assert.NotEqual(5432, connection.Port); Assert.False(connection.Pooling);
        Assert.Equal(connection.Database, await database.ScalarAsync<string>("SELECT current_database()"));
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM rooms"));
        // Porównanie dumpa developerki przed/po pełnej regresji wykonuje dodatkowo audit_state.py.
    }

    private static async Task<Room> SeedHistory(MigrationDatabase database)
    {
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start.AddDays(-1000), MigrationTestData.Start.AddDays(-1000).AddHours(1));
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start.AddDays(1), MigrationTestData.Start.AddDays(1).AddHours(1), "cancelled");
        return data.Room;
    }

    private static Task<string> History(MigrationDatabase database) => database.ScalarAsync<string>("SELECT json_agg(r ORDER BY id)::text FROM reservations r");

    private static async Task SeedRooms(MigrationDatabase database)
    {
        await using var context = database.CreateContext();
        var rows = new[] { Room.Create("Wisła mała", null, "Piętro 1", 10, MigrationTestData.Clock),
            Room.Create("Wisła duża", null, "Piętro 2", 20, MigrationTestData.Clock),
            Room.Create("Wisła archiwalna", null, "Piętro 2", 30, MigrationTestData.Clock),
            Room.Create("Odra", null, "Parter", 20, MigrationTestData.Clock),
            Room.Create("Dosłowna %_\\", null, "Piętro 2", 20, MigrationTestData.Clock) };
        rows[2].Archive(); context.Rooms.AddRange(rows); await context.SaveChangesAsync();
    }

    private static ServiceProvider Services(MigrationDatabase database, bool authenticated = true, bool administrator = true,
        SqlObserver? observer = null, Func<Task>? afterRead = null, Func<Task>? afterNameCheck = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Roomify"] = database.ConnectionString }).Build();
        var services = new ServiceCollection().AddLogging();
        // W testach rezerwacji to rzeczywisty użytkownik utworzony przez SeedHistory.
        using var context = database.CreateContext();
        var id = context.Users.Select(x => x.Id).FirstOrDefault(); if (id == Guid.Empty) id = Guid.NewGuid();
        services.AddSingleton<ICurrentUser>(new TrustedUser(authenticated ? id : null, administrator));
        services.AddSingleton(MigrationTestData.Clock); services.AddApplication().AddInfrastructure(configuration);
        if (observer is not null) services.ConfigureDbContext<RoomifyDbContext>((_, options) => options.AddInterceptors(observer));
        if (afterRead is not null || afterNameCheck is not null) services.AddScoped<IRoomRepository>(sp => new RepositoryHooks(new EfRoomRepository(sp.GetRequiredService<RoomifyDbContext>()), afterRead, afterNameCheck));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class TrustedUser(Guid? id, bool administrator) : ICurrentUser { public Guid? UserId => id; public bool IsActive => true; public bool IsAdministrator => administrator; }
    private sealed class AsyncGate
    {
        private int arrived;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal async Task WaitAsync() { if (Interlocked.Increment(ref arrived) == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
    }
    private sealed class RepositoryHooks(IRoomRepository inner, Func<Task>? afterRead, Func<Task>? afterNameCheck) : IRoomRepository
    {
        public async Task<Room?> FindForUpdateAsync(Guid id, CancellationToken ct) { var room = await inner.FindForUpdateAsync(id, ct); if (afterRead is not null) await afterRead(); return room; }
        public async Task<bool> NameExistsAsync(string name, Guid? excluded, CancellationToken ct) { var exists = await inner.NameExistsAsync(name, excluded, ct); if (afterNameCheck is not null) await afterNameCheck(); return exists; }
        public Task<Room?> FindByIdAsync(Guid id, CancellationToken ct) => inner.FindByIdAsync(id, ct);
        public Task<RoomWriteOutcome> AddAsync(Room room, CancellationToken ct) => inner.AddAsync(room, ct);
        public Task<RoomWriteOutcome> SaveAsync(Room room, CancellationToken ct) => inner.SaveAsync(room, ct);
        public Task<RoomPage> SearchAsync(RoomSearch query, CancellationToken ct) => inner.SearchAsync(query, ct);
    }
    private sealed class SqlObserver : DbCommandInterceptor
    {
        internal ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Commands.Enqueue(command.CommandText); return ValueTask.FromResult(result); }
    }
}
