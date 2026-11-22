using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Roomify.Domain.Errors;
using Roomify.Domain.Reservations;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "PersistenceMetadata")]
[Trait("RequiresDocker", "false")]
public sealed class PersistenceMaterializationTests
{
    [Theory]
    [InlineData("active", ReservationStatus.Active)]
    [InlineData("cancelled", ReservationStatus.Cancelled)]
    public void Reservation_MaterializesHistoricalUtcPeriodAndStatusThroughRealEfQuery(string storage, ReservationStatus expected)
    {
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var start = new DateTimeOffset(2020, 6, 15, 6, 0, 0, TimeSpan.Zero);
        var created = start.AddDays(-1);
        using var rows = ReservationRows(id, roomId, userId, start, start.AddHours(1), storage, created, created.AddMinutes(1));
        var transport = new ReadOnlyRowsInterceptor(rows);
        using var context = CreateContext(transport);

        var reservation = context.Reservations.Single();

        Assert.Equal(id, reservation.Id);
        Assert.Equal(roomId, reservation.RoomId);
        Assert.Equal(userId, reservation.UserId);
        Assert.Equal("Odczyt historii", reservation.Title);
        Assert.Equal(expected, reservation.Status);
        Assert.Equal(created, reservation.CreatedAt);
        Assert.Equal(created.AddMinutes(1), reservation.UpdatedAt);
        Assert.Equal(new ReservationPeriod(start, start.AddHours(1)), reservation.Period);
        Assert.Equal(TimeSpan.Zero, reservation.StartAt.Offset);
        Assert.Equal(TimeSpan.Zero, reservation.EndAt.Offset);
        Assert.Equal(TimeSpan.FromHours(1), reservation.Period.Duration);
        Assert.Equal(EntityState.Unchanged, context.Entry(reservation).State);
        Assert.Equal(1, transport.ExecutedReads);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void Room_MaterializesArchivedRoomAndKeepsStoredIdentity()
    {
        var id = Guid.NewGuid();
        var created = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var rows = new DataTable();
        rows.Columns.Add("id", typeof(Guid));
        rows.Columns.Add("name", typeof(string));
        rows.Columns.Add("description", typeof(string));
        rows.Columns.Add("location", typeof(string));
        rows.Columns.Add("capacity", typeof(int));
        rows.Columns.Add("is_active", typeof(bool));
        rows.Columns.Add("created_at", typeof(DateTimeOffset));
        rows.Columns.Add("xmin", typeof(uint));
        rows.Rows.Add(id, "Wisła", DBNull.Value, "Piętro 1", 12, false, created, 1u);
        using var context = CreateContext(new ReadOnlyRowsInterceptor(rows));

        var room = context.Rooms.Single();

        Assert.Equal(id, room.Id);
        Assert.Equal("Wisła", room.Name);
        Assert.Null(room.Description);
        Assert.Equal("Piętro 1", room.Location);
        Assert.Equal(12, room.Capacity);
        Assert.False(room.IsActive);
        Assert.Equal(created, room.CreatedAt);
    }

    [Fact]
    public void ChangeTracking_DetectsBothScalarEndpointsAfterDomainChange()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var period = new ReservationPeriod(clock.GetUtcNow().AddDays(1).AddHours(9), clock.GetUtcNow().AddDays(1).AddHours(10));
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), "Spotkanie", period, clock);
        using var context = PersistenceModelTests.CreateContext();
        context.Attach(reservation);
        var next = new ReservationPeriod(period.Start.AddHours(1), period.End.AddHours(2));

        reservation.ChangePeriod(next, clock);
        context.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Modified, context.Entry(reservation).State);
        Assert.True(context.Entry(reservation).Property(x => x.StartAt).IsModified);
        Assert.True(context.Entry(reservation).Property(x => x.EndAt).IsModified);
        Assert.Equal(period.Start, context.Entry(reservation).Property(x => x.StartAt).OriginalValue);
        Assert.Equal(next.Start, context.Entry(reservation).Property(x => x.StartAt).CurrentValue);
        Assert.Equal(next, reservation.Period);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Materialization_DoesNotBypassInvalidPeriodInvariant(int durationMinutes)
    {
        var start = new DateTimeOffset(2020, 1, 1, 9, 0, 0, TimeSpan.Zero);
        using var rows = ReservationRows(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start,
            start.AddMinutes(durationMinutes), "active", start.AddDays(-1), null);
        using var context = CreateContext(new ReadOnlyRowsInterceptor(rows));
        var error = Assert.Throws<DomainException>(() => context.Reservations.Single());
        Assert.Equal("BR-001", error.Code);
    }

    private static RoomifyDbContext CreateContext(ReadOnlyRowsInterceptor transport) => new(
        new DbContextOptionsBuilder<RoomifyDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=materialization_test;Username=metadata_test")
            .AddInterceptors(transport, new SuppressPhysicalConnection()).Options);

    private static DataTable ReservationRows(Guid id, Guid roomId, Guid userId, DateTimeOffset start,
        DateTimeOffset end, string status, DateTimeOffset created, DateTimeOffset? updated)
    {
        var rows = new DataTable();
        rows.Columns.Add("id", typeof(Guid));
        rows.Columns.Add("room_id", typeof(Guid));
        rows.Columns.Add("user_id", typeof(Guid));
        rows.Columns.Add("title", typeof(string));
        rows.Columns.Add("start_at", typeof(DateTimeOffset));
        rows.Columns.Add("end_at", typeof(DateTimeOffset));
        rows.Columns.Add("status", typeof(string));
        rows.Columns.Add("created_at", typeof(DateTimeOffset));
        rows.Columns.Add("updated_at", typeof(DateTimeOffset));
        rows.Columns.Add("xmin", typeof(uint));
        rows.Rows.Add(id, roomId, userId, "Odczyt historii", start, end, status, created, (object?)updated ?? DBNull.Value, 1u);
        return rows;
    }

    // Przechwytujemy wyłącznie transport: Npgsql i rzeczywisty materializator EF pozostają aktywne.
    // Nie jest to provider EF InMemory ani potwierdzenie wykonania SQL na PostgreSQL.
    private sealed class SuppressPhysicalConnection : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result) => InterceptionResult.Suppress();
    }

    private sealed class ReadOnlyRowsInterceptor(DataTable rows) : DbCommandInterceptor
    {
        public int ExecutedReads { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Assert.StartsWith("SELECT", command.CommandText.TrimStart());
            ExecutedReads++;
            // EF odczytuje kolumny w kolejności wygenerowanego SELECT, nie kolejności definicji DataTable.
            var columns = System.Text.RegularExpressions.Regex.Matches(command.CommandText.Split("FROM")[0], @"\w+\.(\w+)")
                .Select(x => x.Groups[1].Value).ToArray();
            return InterceptionResult<DbDataReader>.SuppressWithResult(new DataView(rows).ToTable(false, columns).CreateDataReader());
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
