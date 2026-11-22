using Roomify.Domain.Reservations;

namespace Roomify.Application.Reservations;

internal static class ReservationSaveResults
{
    internal static ReservationOperationResult From(ReservationWriteOutcome outcome, Reservation reservation) => outcome switch
    {
        ReservationWriteOutcome.Saved => ReservationOperationResult.Created(reservation),
        ReservationWriteOutcome.Conflict => ReservationOperationResult.Failed(ReservationOperationStatus.Conflict, "reservation.conflict"),
        ReservationWriteOutcome.ConcurrentModification => ReservationOperationResult.Failed(ReservationOperationStatus.ConcurrentModification, "reservation.concurrentModification"),
        _ => ReservationOperationResult.Failed(ReservationOperationStatus.InfrastructureFailure, "reservation.storageFailure"),
    };
}
