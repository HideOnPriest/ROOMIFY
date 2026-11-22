using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roomify.Application.Reservations;
using Roomify.Application.Rooms;
using Roomify.Domain.Rooms;

namespace Roomify.Infrastructure.Persistence.Repositories;

public sealed class EfRoomRepository(RoomifyDbContext context) : IRoomRepository
{
    public Task<Room?> FindForUpdateAsync(Guid roomId, CancellationToken cancellationToken)
        => ReadAsync(() => context.Rooms.SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken));

    public Task<bool> NameExistsAsync(string name, Guid? excludedRoomId, CancellationToken cancellationToken)
        => ReadAsync(() => context.Rooms.AsNoTracking().AnyAsync(x => x.Name == name
            && (!excludedRoomId.HasValue || x.Id != excludedRoomId.Value), cancellationToken));

    public async Task<RoomPage> SearchAsync(RoomSearch query, CancellationToken cancellationToken)
    {
        try
        {
            var source = context.Rooms.AsNoTracking();
            if (query.IsActive is { } active) source = source.Where(x => x.IsActive == active);
            if (query.MinCapacity is { } capacity) source = source.Where(x => x.Capacity >= capacity);
            // PG18 LIKE obsługuje istniejącą kolację ICU bez rozróżniania wielkości liter.
            if (!string.IsNullOrEmpty(query.Search))
            {
                var pattern = Pattern(query.Search);
                source = source.Where(x => EF.Functions.Like(x.Name, pattern, "\\"));
            }
            if (!string.IsNullOrEmpty(query.Location))
            {
                var pattern = Pattern(query.Location);
                source = source.Where(x => EF.Functions.ILike(x.Location, pattern, "\\"));
            }
            var count = await source.CountAsync(cancellationToken);
            var descending = query.SortDirection == RoomSortDirection.Descending;
            IOrderedQueryable<Room> ordered = query.SortBy switch
            {
                RoomSortBy.Name => descending ? source.OrderByDescending(x => x.Name) : source.OrderBy(x => x.Name),
                RoomSortBy.Location => descending ? source.OrderByDescending(x => x.Location) : source.OrderBy(x => x.Location),
                RoomSortBy.Capacity => descending ? source.OrderByDescending(x => x.Capacity) : source.OrderBy(x => x.Capacity),
                RoomSortBy.CreatedAt => descending ? source.OrderByDescending(x => x.CreatedAt) : source.OrderBy(x => x.CreatedAt),
                _ => throw new ArgumentOutOfRangeException(nameof(query))
            };
            var items = await ordered.ThenBy(x => x.Id).Skip(query.Offset).Take(query.Limit)
                .Select(x => new RoomDetails(x.Id, x.Name, x.Description, x.Location, x.Capacity, x.IsActive, x.CreatedAt))
                .ToListAsync(cancellationToken);
            return new(items, count, query.Offset / query.Limit + 1, query.Limit);
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error)) { throw new ReservationStorageException(); }
    }

    private static string Pattern(string value) => "%" + value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    public Task<RoomWriteOutcome> AddAsync(Room room, CancellationToken cancellationToken)
    {
        context.Rooms.Add(room);
        return PersistAsync(room, cancellationToken);
    }

    public Task<RoomWriteOutcome> SaveAsync(Room room, CancellationToken cancellationToken)
    {
        if (context.Entry(room).State == EntityState.Detached)
            throw new InvalidOperationException("Zapis wymaga encji odczytanej ze śledzeniem w tym zakresie.");
        return PersistAsync(room, cancellationToken);
    }

    private async Task<RoomWriteOutcome> PersistAsync(Room room, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return RoomWriteOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Entry(room).State = EntityState.Detached;
            return RoomWriteOutcome.ConcurrentModification;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_rooms_name" })
        {
            context.Entry(room).State = EntityState.Detached;
            return RoomWriteOutcome.DuplicateName;
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error))
        {
            context.Entry(room).State = EntityState.Detached;
            throw new ReservationStorageException();
        }
        catch (OperationCanceledException)
        {
            context.Entry(room).State = EntityState.Detached;
            throw;
        }
    }

    private static async Task<T> ReadAsync<T>(Func<Task<T>> read)
    {
        try { return await read(); }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error)) { throw new ReservationStorageException(); }
    }

    public async Task<Room?> FindByIdAsync(Guid roomId, CancellationToken cancellationToken)
    {
        try
        {
            return await context.Rooms.AsNoTracking().SingleOrDefaultAsync(room => room.Id == roomId, cancellationToken);
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error))
        {
            throw new ReservationStorageException();
        }
    }
}
