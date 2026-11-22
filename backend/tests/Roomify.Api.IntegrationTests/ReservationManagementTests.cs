using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roomify.Application;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Domain.Reservations;
using Roomify.Infrastructure;
using Roomify.Infrastructure.Persistence;
using Roomify.Infrastructure.Persistence.Repositories;
using Xunit.Abstractions;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "ReservationManagement")]
[Trait("RequiresDocker", "true")]
public sealed class ReservationManagementTests(PostgreSqlFixture fixture, ITestOutputHelper output) : IClassFixture<PostgreSqlFixture>
{
    [Fact(DisplayName = "INT-UPD-001/007: Edycja tytułu i terminu zachowuje ID, salę i właściciela")]
    public async Task Update_PersistsBothFieldsAndPreservesIdentity()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var reservation = await Seed(database);
        var observer = new SqlObserver();
        await using var services = Services(database, reservation.UserId, observer: observer);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var loaded = await repository.FindByIdAsync(reservation.Id, default);
        var beforeVersion = context.Entry(loaded!).Property<uint>("xmin").OriginalValue;
        Assert.NotEqual(0u, beforeVersion);
        var result = await scope.ServiceProvider.GetRequiredService<UpdateReservationService>().UpdateAsync(Update(reservation));
        Assert.Equal(ReservationOperationStatus.Success, result.Status);
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.SingleAsync();
        Assert.Equal(reservation.Id, persisted.Id);
        Assert.Equal(reservation.RoomId, persisted.RoomId);
        Assert.Equal(reservation.UserId, persisted.UserId);
        Assert.Equal(reservation.CreatedAt, persisted.CreatedAt);
        Assert.Equal("Zmieniony tytuł", persisted.Title);
        Assert.Equal(Update(reservation).StartAt, persisted.StartAt);
        Assert.Equal(Update(reservation).EndAt, persisted.EndAt);
        Assert.Equal(TimeSpan.Zero, persisted.StartAt.Offset);
        Assert.Equal(ReservationStatus.Active, persisted.Status);
        Assert.Equal(MigrationTestData.Clock.GetUtcNow(), persisted.UpdatedAt);
        Assert.NotEqual(beforeVersion, read.Entry(persisted).Property<uint>("xmin").CurrentValue);
        var sql = Assert.Single(observer.Commands, command => command.StartsWith("UPDATE ", StringComparison.Ordinal));
        Assert.Contains("AND xmin =", sql);
        Assert.Contains("RETURNING xmin", sql);
    }

    [Theory(DisplayName = "INT-UPD-002: Konflikt edycji nie zmienia danych — precheck lub exclusion")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_RejectsTermConflictAndPreservesStoredData(bool afterPrecheck)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var reservation = await Seed(database);
        async Task Occupy()
        {
            await MigrationTestData.InsertAsync(database, reservation.RoomId, reservation.UserId,
                Update(reservation).StartAt, Update(reservation).EndAt);
        }
        if (!afterPrecheck) await Occupy();
        await using var services = Services(database, reservation.UserId, afterCheck: afterPrecheck ? Occupy : null);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<UpdateReservationService>().UpdateAsync(Update(reservation));
        AssertFailure(result, ReservationOperationStatus.Conflict, "reservation.conflict");
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.SingleAsync(x => x.Id == reservation.Id);
        Assert.Equal(reservation.Title, persisted.Title);
        Assert.Equal(reservation.Period, persisted.Period);
        Assert.Null(persisted.UpdatedAt);
        Assert.Equal(2, await read.Reservations.CountAsync());
        if (afterPrecheck) Assert.Empty(scope.ServiceProvider.GetRequiredService<RoomifyDbContext>().ChangeTracker.Entries<Reservation>());
    }

    [Fact(DisplayName = "INT-UPD-003: Ten sam termin wyklucza własne ID i pozwala zmienić tytuł")]
    public async Task Update_SamePeriodDoesNotConflictWithItself()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var reservation = await Seed(database);
        var observer = new SqlObserver();
        await using var services = Services(database, reservation.UserId, observer: observer);
        await using var scope = services.CreateAsyncScope();
        var request = Update(reservation) with { StartAt = reservation.StartAt, EndAt = reservation.EndAt };
        Assert.Equal(ReservationOperationStatus.Success, (await scope.ServiceProvider.GetRequiredService<UpdateReservationService>().UpdateAsync(request)).Status);
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.SingleAsync();
        Assert.Equal(reservation.Period, persisted.Period);
        Assert.Equal("Zmieniony tytuł", persisted.Title);
        var sql = Assert.Single(observer.Commands, text => text.Contains("EXISTS", StringComparison.Ordinal));
        Assert.Contains("id <>", sql);
        Assert.Contains("start_at <", sql);
        Assert.Contains("end_at >", sql);
    }

    [Fact(DisplayName = "INT-UPD-004: Anulowana rezerwacja nie może być edytowana")]
    public async Task Update_CancelledIsRejectedWithoutChangingHistory()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var reservation = await Seed(database, cancelled: true);
        await using var services = Services(database, reservation.UserId);
        await using var scope = services.CreateAsyncScope();
        AssertFailure(await scope.ServiceProvider.GetRequiredService<UpdateReservationService>().UpdateAsync(Update(reservation)),
            ReservationOperationStatus.AlreadyCancelled, "reservation.alreadyCancelled");
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.SingleAsync();
        Assert.Equal(reservation.Title, persisted.Title);
        Assert.Equal(reservation.Period, persisted.Period);
        Assert.Equal(reservation.UpdatedAt, persisted.UpdatedAt);
    }

    [Fact(DisplayName = "INT-CAN-001/002/003: Anulowanie zachowuje rekord i zwalnia termin")]
    public async Task Cancel_PersistsHistoryAndAllowsNewBooking()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var reservation = await Seed(database);
        await using var services = Services(database, reservation.UserId);
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CancelReservationService>().CancelAsync(new(reservation.Id));
        Assert.Equal(ReservationOperationStatus.Success, result.Status);
        Assert.Equal(ReservationStatus.Cancelled, result.Reservation!.Status);
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.SingleAsync();
        Assert.Equal(reservation.Id, persisted.Id);
        Assert.Equal(reservation.Title, persisted.Title);
        Assert.Equal(reservation.Period, persisted.Period);
        Assert.Equal(reservation.UserId, persisted.UserId);
        Assert.Equal(ReservationStatus.Cancelled, persisted.Status);
        Assert.NotNull(persisted.UpdatedAt);
        var created = await scope.ServiceProvider.GetRequiredService<CreateReservationService>().CreateAsync(new(
            reservation.RoomId, "Nowe spotkanie", reservation.StartAt, reservation.EndAt));
        Assert.Equal(ReservationOperationStatus.Success, created.Status);
        Assert.NotEqual(reservation.Id, created.Reservation!.Id);
        Assert.Equal(2, await read.Reservations.CountAsync());
    }

    [Fact(DisplayName = "INT-CAN-004: Powtórne anulowanie nie zapisuje ani nie zmienia xmin/UpdatedAt")]
    public async Task Cancel_RepeatIsExplicitAndDoesNotWrite()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var reservation = await Seed(database, cancelled: true);
        await using var before = database.CreateContext();
        var original = await before.Reservations.SingleAsync();
        var version = before.Entry(original).Property<uint>("xmin").CurrentValue;
        var observer = new SqlObserver();
        await using var services = Services(database, reservation.UserId, observer: observer);
        await using var scope = services.CreateAsyncScope();
        AssertFailure(await scope.ServiceProvider.GetRequiredService<CancelReservationService>().CancelAsync(new(reservation.Id)),
            ReservationOperationStatus.AlreadyCancelled, "reservation.alreadyCancelled");
        Assert.DoesNotContain(observer.Commands, command => command.StartsWith("UPDATE", StringComparison.Ordinal));
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.SingleAsync();
        Assert.Equal(version, read.Entry(persisted).Property<uint>("xmin").CurrentValue);
        Assert.Equal(original.UpdatedAt, persisted.UpdatedAt);
    }

    [Theory(DisplayName = "INT-SEC-001/002/003/004: Administrator może zarządzać cudzą rezerwacją, zwykły użytkownik nie")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Security_TrustedAdministratorAndOwnerAreRequired(bool cancel, bool administrator)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var reservation = await Seed(database);
        var other = await MigrationTestData.SeedAsync(database);
        await using var services = Services(database, other.User.Id, administrator);
        await using var scope = services.CreateAsyncScope();
        var result = cancel
            ? await scope.ServiceProvider.GetRequiredService<CancelReservationService>().CancelAsync(new(reservation.Id))
            : await scope.ServiceProvider.GetRequiredService<UpdateReservationService>().UpdateAsync(Update(reservation));
        if (administrator) Assert.Equal(ReservationOperationStatus.Success, result.Status);
        else AssertFailure(result, ReservationOperationStatus.Forbidden, "reservation.forbidden");
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.SingleAsync();
        Assert.Equal(reservation.UserId, persisted.UserId);
        Assert.Equal(reservation.Id, persisted.Id);
        Assert.Equal(administrator && cancel ? ReservationStatus.Cancelled : ReservationStatus.Active, persisted.Status);
        Assert.Equal(administrator && !cancel ? "Zmieniony tytuł" : reservation.Title, persisted.Title);
        if (!administrator) Assert.Null(persisted.UpdatedAt);
    }

    [Theory(DisplayName = "INT-UPD-005/006 i INT-CAN-005: Równoczesne zmiany tego samego xmin nie nadpisują danych")]
    [InlineData("update-update", 1)]
    [InlineData("update-update", 2)]
    [InlineData("update-update", 3)]
    [InlineData("update-cancel", 1)]
    [InlineData("update-cancel", 2)]
    [InlineData("update-cancel", 3)]
    [InlineData("cancel-cancel", 1)]
    [InlineData("cancel-cancel", 2)]
    [InlineData("cancel-cancel", 3)]
    public async Task ConcurrentManagement_DetectsStaleVersionAndPreservesWinningChange(string scenario, int iteration)
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture);
        var reservation = await Seed(database);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participants = 0;
        async Task Barrier()
        {
            if (Interlocked.Increment(ref participants) == 2) ready.SetResult();
            await ready.Task.WaitAsync(timeout.Token);
        }
        await using var services = Services(database, reservation.UserId, afterRead: Barrier);
        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        var firstContext = first.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<RoomifyDbContext>();
        Assert.NotSame(firstContext, secondContext);
        Assert.NotSame(firstContext.Database.GetDbConnection(), secondContext.Database.GetDbConnection());
        var a = scenario == "cancel-cancel"
            ? first.ServiceProvider.GetRequiredService<CancelReservationService>().CancelAsync(new(reservation.Id), timeout.Token)
            : first.ServiceProvider.GetRequiredService<UpdateReservationService>().UpdateAsync(Update(reservation) with { Title = "Wygrywa A" }, timeout.Token);
        var b = scenario == "update-update"
            ? second.ServiceProvider.GetRequiredService<UpdateReservationService>().UpdateAsync(Update(reservation) with
                { Title = "Wygrywa B", StartAt = reservation.StartAt.AddHours(4), EndAt = reservation.EndAt.AddHours(4) }, timeout.Token)
            : second.ServiceProvider.GetRequiredService<CancelReservationService>().CancelAsync(new(reservation.Id), timeout.Token);
        var results = await Task.WhenAll(a, b).WaitAsync(timeout.Token);
        Assert.Equal(2, participants);
        var winner = Assert.Single(results, result => result.Status == ReservationOperationStatus.Success);
        AssertFailure(Assert.Single(results, result => result.Status != ReservationOperationStatus.Success),
            ReservationOperationStatus.ConcurrentModification, "reservation.concurrentModification");
        await using var read = database.CreateContext();
        var persisted = await read.Reservations.SingleAsync();
        Assert.Equal(winner.Reservation!.Title, persisted.Title);
        Assert.Equal(winner.Reservation.StartAt, persisted.StartAt);
        Assert.Equal(winner.Reservation.EndAt, persisted.EndAt);
        Assert.Equal(winner.Reservation.Status, persisted.Status);
        Assert.Equal(winner.Reservation.UpdatedAt, persisted.UpdatedAt);
        Assert.Equal(reservation.Id, persisted.Id);
        Assert.Equal(reservation.UserId, persisted.UserId);
        Assert.Equal(reservation.RoomId, persisted.RoomId);
        Assert.Equal(reservation.CreatedAt, persisted.CreatedAt);
        Assert.Single(firstContext.ChangeTracker.Entries<Reservation>().Concat(secondContext.ChangeTracker.Entries<Reservation>()));
        output.WriteLine("{0}, próba {1}: A={2}, B={3}; rekord=1, stan={4}, tytuł={5}",
            scenario, iteration, results[0].Status, results[1].Status, persisted.Status, persisted.Title);
        if (scenario == "cancel-cancel")
        {
            await using var retryServices = Services(database, reservation.UserId);
            await using var retry = retryServices.CreateAsyncScope();
            AssertFailure(await retry.ServiceProvider.GetRequiredService<CancelReservationService>().CancelAsync(new(reservation.Id)),
                ReservationOperationStatus.AlreadyCancelled, "reservation.alreadyCancelled");
        }
    }

    [Fact]
    public async Task XminMigration_UpgradeAndDowngradePreserveNativeColumnAndExistingRows()
    {
        await using var database = await MigrationDatabase.CreateAsync(fixture, migrate: false);
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync("20261018093445_InitialCreate");
        await Seed(database);
        var before = await database.ScalarAsync<string>("SELECT row_to_json(r)::text FROM reservations r");
        var xmin = await database.ScalarAsync<uint>("SELECT xmin FROM reservations");
        await context.GetService<IMigrator>().MigrateAsync("20261018104209_ReservationOptimisticConcurrency");
        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(before, await database.ScalarAsync<string>("SELECT row_to_json(r)::text FROM reservations r"));
        Assert.Equal(xmin, await database.ScalarAsync<uint>("SELECT xmin FROM reservations"));
        await context.GetService<IMigrator>().MigrateAsync("20261018093445_InitialCreate");
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(before, await database.ScalarAsync<string>("SELECT row_to_json(r)::text FROM reservations r"));
        Assert.Equal(xmin, await database.ScalarAsync<uint>("SELECT xmin FROM reservations"));
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static async Task<Reservation> Seed(MigrationDatabase database, bool cancelled = false)
    {
        var data = await MigrationTestData.SeedAsync(database);
        var reservation = MigrationTestData.Reservation(data.Room.Id, data.User.Id);
        if (cancelled) reservation.Cancel(MigrationTestData.Clock);
        await using var context = database.CreateContext();
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();
        return reservation;
    }

    private static UpdateReservationRequest Update(Reservation reservation) => new(reservation.Id, "  Zmieniony tytuł  ", reservation.StartAt.AddHours(2), reservation.EndAt.AddHours(2));

    private static ServiceProvider Services(MigrationDatabase database, Guid userId, bool administrator = false,
        SqlObserver? observer = null, Func<Task>? afterRead = null, Func<Task>? afterCheck = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Roomify"] = database.ConnectionString }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<ICurrentUser>(new TrustedUser(userId, administrator));
        services.AddSingleton(MigrationTestData.Clock);
        services.AddApplication().AddInfrastructure(configuration);
        if (observer is not null) services.ConfigureDbContext<RoomifyDbContext>((_, options) => options.AddInterceptors(observer));
        if (afterRead is not null || afterCheck is not null) services.AddScoped<IReservationRepository>(provider => new RepositoryHooks(
            new EfReservationRepository(provider.GetRequiredService<RoomifyDbContext>()), afterRead, afterCheck));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static void AssertFailure(ReservationOperationResult result, ReservationOperationStatus status, string code)
    {
        Assert.Equal(status, result.Status);
        Assert.Equal(code, result.ErrorCode);
        Assert.Null(result.Reservation);
    }

    private sealed class TrustedUser(Guid id, bool administrator) : ICurrentUser
    {
        public Guid? UserId => id; public bool IsActive => true;
        public bool IsAdministrator => administrator;
    }

    private sealed class RepositoryHooks(IReservationRepository inner, Func<Task>? afterRead, Func<Task>? afterCheck) : IReservationRepository
    {
        public async Task<Reservation?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var reservation = await inner.FindByIdAsync(id, cancellationToken);
            if (afterRead is not null) await afterRead();
            return reservation;
        }
        public async Task<bool> HasActiveConflictExcludingAsync(Guid roomId, ReservationPeriod period, Guid excludedId, CancellationToken cancellationToken)
        {
            var conflict = await inner.HasActiveConflictExcludingAsync(roomId, period, excludedId, cancellationToken);
            if (!conflict && afterCheck is not null) await afterCheck();
            return conflict;
        }
        public Task<bool> HasActiveConflictAsync(Guid roomId, ReservationPeriod period, CancellationToken cancellationToken)
            => inner.HasActiveConflictAsync(roomId, period, cancellationToken);
        public Task<ReservationWriteOutcome> AddAsync(Reservation reservation, CancellationToken cancellationToken) => inner.AddAsync(reservation, cancellationToken);
        public Task<ReservationWriteOutcome> SaveAsync(Reservation reservation, CancellationToken cancellationToken) => inner.SaveAsync(reservation, cancellationToken);
    }

    private sealed class SqlObserver : DbCommandInterceptor
    {
        internal ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
