using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;

namespace Roomify.Application.Reservations;

public interface IRoomRepository
{
    Task<Room?> FindByIdAsync(Guid roomId, CancellationToken cancellationToken);
    Task<Room?> FindForUpdateAsync(Guid roomId, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(string name, Guid? excludedRoomId, CancellationToken cancellationToken);
    Task<Rooms.RoomWriteOutcome> AddAsync(Room room, CancellationToken cancellationToken);
    Task<Rooms.RoomWriteOutcome> SaveAsync(Room room, CancellationToken cancellationToken);
    Task<Rooms.RoomPage> SearchAsync(Rooms.RoomSearch query, CancellationToken cancellationToken);
}

public interface IReservationRepository
{
    Task<bool> HasActiveConflictAsync(Guid roomId, ReservationPeriod period, CancellationToken cancellationToken);
    Task<ReservationWriteOutcome> AddAsync(Reservation reservation, CancellationToken cancellationToken);
    Task<Reservation?> FindByIdAsync(Guid reservationId, CancellationToken cancellationToken);
    Task<bool> HasActiveConflictExcludingAsync(Guid roomId, ReservationPeriod period, Guid excludedReservationId, CancellationToken cancellationToken);
    Task<ReservationWriteOutcome> SaveAsync(Reservation reservation, CancellationToken cancellationToken);
}

public enum ReservationWriteOutcome { Saved, Conflict, ConcurrentModification }

/// <summary>Kontrolowany błąd granicy persystencji bez SQL, connection stringa i wyjątków providera.</summary>
public sealed class ReservationStorageException() : Exception("Nie można wykonać operacji na magazynie rezerwacji.");
