using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roomify.Application;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Domain.Reservations;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Persistence;
using Roomify.Infrastructure.Persistence.Repositories;
using Xunit.Abstractions;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "ReservationApplication")]
[Trait("RequiresDocker", "true")]
public sealed class ReservationApplicationTests(PostgreSqlFixture fixture, ITestOutputHelper output) : IClassFixture<PostgreSqlFixture>
{
    [Fact(DisplayName = "INT-RES-001: Wolny termin zapisuje poprawną rezerwację przez Application i EF")]
    public async Task INT001_FreePeriodCreatesPersistedReservation()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        var observer = new DatabaseObserver();
        await using var services = Services(database, data.User.Id, observer);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(Request(data.Room.Id));
        Assert.Equal(ReservationOperationStatus.Success, result.Status);
        Assert.NotNull(result.Reservation);
        Assert.Null(result.ErrorCode);
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.AsNoTracking().SingleAsync();
        Assert.Equal(result.Reservation.Id, persisted.Id);
        Assert.Equal(data.User.Id, persisted.UserId);
        Assert.Equal(data.Room.Id, persisted.RoomId);
        Assert.Equal("Spotkanie aplikacyjne", persisted.Title);
        Assert.Equal(MigrationTestData.Start, persisted.StartAt);
        Assert.Equal(MigrationTestData.Start.AddHours(1), persisted.EndAt);
        Assert.Equal(TimeSpan.Zero, persisted.StartAt.Offset);
        Assert.Equal(ReservationStatus.Active, persisted.Status);
        Assert.Null(persisted.UpdatedAt);
        Assert.Equal(MigrationTestData.Clock.GetUtcNow(), persisted.CreatedAt);
        // Prawdziwy provider wykonuje EXISTS z warunkami zakresu, nie SELECT całej listy.
        var sql = Assert.Single(observer.Commands, sql => sql.Contains("EXISTS", StringComparison.Ordinal));
        Assert.Contains("room_id", sql);
        Assert.Contains("start_at <", sql);
        Assert.Contains("end_at >", sql);
        Assert.Contains("'active'", sql);
    }

    [Theory(DisplayName = "INT-RES-002: Aktywne nakładanie jest odrzucane przed zapisem")]
    [InlineData(-30, 30)]
    [InlineData(30, 90)]
    [InlineData(-30, 90)]
    [InlineData(15, 45)]
    [InlineData(0, 60)]
    public async Task INT002_OverlapsAreRejected(int startMinutes, int endMinutes)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        await using var services = Services(database, data.User.Id);
        await using var scope = services.CreateAsyncScope();
        var request = Request(data.Room.Id) with { StartAt = MigrationTestData.Start.AddMinutes(startMinutes), EndAt = MigrationTestData.Start.AddMinutes(endMinutes) };
        AssertFailure(await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(request), ReservationOperationStatus.Conflict);
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
        Assert.Empty(scope.ServiceProvider.GetRequiredService<RoomifyDbContext>().ChangeTracker.Entries<Reservation>());
    }

    [Theory(DisplayName = "INT-RES-003: Styk z obu stron jest dozwolony")]
    [InlineData(-60, 0)]
    [InlineData(60, 120)]
    public async Task INT003_AdjacentPeriodsAreAllowed(int startMinutes, int endMinutes)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        await using var services = Services(database, data.User.Id);
        await using var scope = services.CreateAsyncScope();
        var request = Request(data.Room.Id) with { StartAt = MigrationTestData.Start.AddMinutes(startMinutes), EndAt = MigrationTestData.Start.AddMinutes(endMinutes) };
        Assert.Equal(ReservationOperationStatus.Success, (await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(request)).Status);
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
    }

    [Fact(DisplayName = "INT-RES-004: Anulowana rezerwacja nie blokuje zapisu")]
    public async Task INT004_CancelledPeriodIsAvailable()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1), "cancelled");
        await using var services = Services(database, data.User.Id);
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(ReservationOperationStatus.Success, (await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(Request(data.Room.Id))).Status);
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations WHERE status='active'"));
    }

    [Theory(DisplayName = "INT-RES-005/006: Sala nieaktywna lub nieistniejąca daje kontrolowany błąd")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task INT005_006_RoomMustExistAndBeActive(bool inactive)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        if (inactive)
        {
            await using var edit = database.CreateContext();
            var room = await edit.Rooms.SingleAsync();
            room.Archive();
            await edit.SaveChangesAsync();
        }
        await using var services = Services(database, data.User.Id);
        await using var scope = services.CreateAsyncScope();
        AssertFailure(await scope.ServiceProvider.GetRequiredService<CreateReservationService>()
            .CreateAsync(Request(inactive ? data.Room.Id : Guid.NewGuid())),
            inactive ? ReservationOperationStatus.RoomInactive : ReservationOperationStatus.RoomNotFound);
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
    }

    [Theory(DisplayName = "INT-RES-007: Dwa przypadki użycia równolegle nie zapisują dwóch kolizji")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task INT007_ConcurrentUseCasesPreserveIntegrity(int iteration)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        var observer = new DatabaseObserver();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;
        async Task Synchronize()
        {
            if (Interlocked.Increment(ref participants) == 2) ready.SetResult();
            await ready.Task.WaitAsync(timeout.Token);
        }
        await using var services = Services(database, data.User.Id, observer, Synchronize);
        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        var firstContext = first.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        Assert.NotSame(firstContext, secondContext);
        Assert.NotSame(firstContext.Database.GetDbConnection(), secondContext.Database.GetDbConnection());
        // Obie prawdziwe kontrole dostępności muszą stwierdzić wolny termin, zanim ruszą INSERT-y.
        var results = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(Request(data.Room.Id), timeout.Token),
            second.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(Request(data.Room.Id), timeout.Token)).WaitAsync(timeout.Token);
        Assert.Equal(2, participants);
        Assert.Single(results, result => result.Status == ReservationOperationStatus.Success);
        var rejected = Assert.Single(results, result => result.Status != ReservationOperationStatus.Success);
        var error = Assert.Single(observer.Errors);
        if (error.State == PostgresErrorCodes.ExclusionViolation)
        {
            Assert.Equal("ex_reservations_active_period", error.Constraint);
            AssertFailure(rejected, ReservationOperationStatus.Conflict);
        }
        else
        {
            Assert.Equal(PostgresErrorCodes.DeadlockDetected, error.State);
            AssertFailure(rejected, ReservationOperationStatus.InfrastructureFailure);
        }
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations WHERE status='active'"));
        output.WriteLine("Próba {0}: A={1}, B={2}; SQLSTATE={3}; aktywne=1", iteration, results[0].Status, results[1].Status, error.State);
    }

    [Fact(DisplayName = "INT-RES-008: 23P01 po nieaktualnym odczycie daje wynik Conflict")]
    public async Task INT008_RacedConstraintViolationIsTranslatedAndDetached()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        var observer = new DatabaseObserver();
        await using var services = Services(database, data.User.Id, observer, async () =>
        {
            // Inne połączenie zajmuje termin po prawdziwym SELECT, przed SaveChanges.
            await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        });
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(Request(data.Room.Id));
        AssertFailure(result, ReservationOperationStatus.Conflict);
        Assert.Equal(("23P01", "ex_reservations_active_period"), Assert.Single(observer.Errors));
        var context = scope.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        Assert.Empty(context.ChangeTracker.Entries<Reservation>());
        Assert.Equal(0, await context.SaveChangesAsync());
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
    }

    [Theory(DisplayName = "INT-RES-009: FK i inny exclusion nie są maskowane jako konflikt terminu")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task INT009_UnrelatedDatabaseErrorIsInfrastructureFailure(bool otherExclusion)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var data = await MigrationTestData.SeedAsync(database);
        var request = Request(data.Room.Id);
        if (otherExclusion)
        {
            await MigrationTestData.InsertAsync(database, data.Room.Id, data.User.Id, MigrationTestData.Start.AddHours(2), MigrationTestData.Start.AddHours(3));
            await database.ExecuteAsync("UPDATE reservations SET title=@title", new NpgsqlParameter("title", request.Title!.Trim()));
            // Ograniczenie istnieje tylko w prywatnej bazie tego negatywnego testu.
            await database.ExecuteAsync("ALTER TABLE reservations ADD CONSTRAINT ex_test_title EXCLUDE USING gist (title WITH =)");
        }
        var observer = new DatabaseObserver();
        await using var services = Services(database, data.User.Id, observer, otherExclusion ? null : async () =>
        {
            await database.ExecuteAsync("DELETE FROM users WHERE id=@id", new NpgsqlParameter("id", data.User.Id));
        });
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(request);
        AssertFailure(result, ReservationOperationStatus.InfrastructureFailure);
        var error = Assert.Single(observer.Errors);
        Assert.Equal(otherExclusion ? "23P01" : "23503", error.State);
        Assert.Equal(otherExclusion ? "ex_test_title" : "fk_reservations_users_user_id", error.Constraint);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<RoomifyDbContext>().ChangeTracker.Entries<Reservation>());
        Assert.Equal(otherExclusion ? 1L : 0L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
    }

    [Fact]
    public async Task DifferentRoom_DoesNotBlockTheSamePeriod()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var first = await MigrationTestData.SeedAsync(database);
        var other = await MigrationTestData.SeedAsync(database);
        await MigrationTestData.InsertAsync(database, other.Room.Id, other.User.Id, MigrationTestData.Start, MigrationTestData.Start.AddHours(1));
        await using var services = Services(database, first.User.Id);
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(ReservationOperationStatus.Success, (await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(Request(first.Room.Id))).Status);
        Assert.Equal(2L, await database.ScalarAsync<long>("SELECT count(*) FROM reservations"));
    }

    private static CreateReservationRequest Request(Guid roomId) => new(roomId, "  Spotkanie aplikacyjne  ", MigrationTestData.Start, MigrationTestData.Start.AddHours(1));

    private static ServiceProvider Services(MigrationDatabase database, Guid userId, DatabaseObserver? observer = null, Func<Task>? afterAvailability = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Roomify"] = database.ConnectionString }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<ICurrentUser>(new TrustedTestUser(userId));
        services.AddSingleton(MigrationTestData.Clock);
        services.AddApplication().AddInfrastructure(configuration);
        if (observer is not null) services.ConfigureDbContext<RoomifyDbContext>((_, options) => options.AddInterceptors(observer));
        if (afterAvailability is not null) services.AddScoped<IReservationRepository>(provider =>
            new AfterAvailabilityRepository(new EfReservationRepository(provider.GetRequiredService<RoomifyDbContext>()), afterAvailability));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static void AssertFailure(ReservationOperationResult result, ReservationOperationStatus expected)
    {
        Assert.Equal(expected, result.Status);
        Assert.Null(result.Reservation);
        Assert.Equal(expected switch
        {
            ReservationOperationStatus.Conflict => "reservation.conflict",
            ReservationOperationStatus.RoomInactive => "reservation.roomInactive",
            ReservationOperationStatus.RoomNotFound => "reservation.roomNotFound",
            ReservationOperationStatus.InfrastructureFailure => "reservation.storageFailure",
            _ => throw new ArgumentOutOfRangeException(nameof(expected)),
        }, result.ErrorCode);
    }

    private sealed class TrustedTestUser(Guid id) : ICurrentUser { public Guid? UserId => id; public bool IsActive => true; }

    private sealed class AfterAvailabilityRepository(IReservationRepository inner, Func<Task> afterAvailability) : IReservationRepository
    {
        public async Task<bool> HasActiveConflictAsync(Guid roomId, ReservationPeriod period, CancellationToken cancellationToken)
        {
            var conflict = await inner.HasActiveConflictAsync(roomId, period, cancellationToken);
            Assert.False(conflict);
            await afterAvailability();
            return conflict;
        }
        public Task<ReservationWriteOutcome> AddAsync(Reservation reservation, CancellationToken cancellationToken)
            => inner.AddAsync(reservation, cancellationToken);
        public Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => inner.FindByIdAsync(id, cancellationToken);
        public Task<bool> HasActiveConflictExcludingAsync(Guid roomId, ReservationPeriod period, Guid excludedId, CancellationToken cancellationToken)
            => inner.HasActiveConflictExcludingAsync(roomId, period, excludedId, cancellationToken);
        public Task<ReservationWriteOutcome> SaveAsync(Reservation reservation, CancellationToken cancellationToken)
            => inner.SaveAsync(reservation, cancellationToken);
    }

    private sealed class DatabaseObserver : DbCommandInterceptor
    {
        internal ConcurrentQueue<string> Commands { get; } = new();
        internal ConcurrentQueue<(string State, string? Constraint)> Errors { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Exception is PostgresException error) Errors.Enqueue((error.SqlState, error.ConstraintName));
            return Task.CompletedTask;
        }
    }
}
