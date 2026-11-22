using Roomify.Domain.Reservations;

namespace Roomify.Application.Reservations;

// UserId celowo nie jest polem żądania; zapewnia go wyłącznie ICurrentUser.
public sealed record CreateReservationRequest(Guid RoomId, string? Title, DateTimeOffset StartAt, DateTimeOffset EndAt);

public enum ReservationOperationStatus
{
    Success, InvalidInput, RoomNotFound, RoomInactive, Conflict, Unauthenticated, InfrastructureFailure,
    ReservationNotFound, Forbidden, AlreadyCancelled, ConcurrentModification,
}

public sealed record ReservationDetails(Guid Id, Guid RoomId, Guid UserId, string Title,
    DateTimeOffset StartAt, DateTimeOffset EndAt, ReservationStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record ReservationOperationResult
{
    public ReservationOperationStatus Status { get; }
    public ReservationDetails? Reservation { get; }
    public string? ErrorCode { get; }

    private ReservationOperationResult(ReservationOperationStatus status, ReservationDetails? reservation, string? errorCode)
        => (Status, Reservation, ErrorCode) = (status, reservation, errorCode);

    internal static ReservationOperationResult Created(Reservation reservation) => new(ReservationOperationStatus.Success,
        new(reservation.Id, reservation.RoomId, reservation.UserId, reservation.Title, reservation.StartAt,
            reservation.EndAt, reservation.Status, reservation.CreatedAt, reservation.UpdatedAt), null);

    internal static ReservationOperationResult Failed(ReservationOperationStatus status, string code) => new(status, null, code);
}
