using Roomify.Application.Abstractions;
using Roomify.Domain.Errors;
using Roomify.Domain.Reservations;

namespace Roomify.Application.Reservations;

public sealed class CreateReservationService(IRoomRepository rooms, IReservationRepository reservations,
    ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<ReservationOperationResult> CreateAsync(CreateReservationRequest? request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = currentUser.UserId;
        if (ApplicationAccess.Check(currentUser) is { } denied)
            return ReservationOperationResult.Failed(denied == AccessFailure.Unauthenticated
                ? ReservationOperationStatus.Unauthenticated : ReservationOperationStatus.Forbidden,
                denied == AccessFailure.Unauthenticated ? "reservation.unauthenticated" : "reservation.forbidden");
        if (request is null)
            return ReservationOperationResult.Failed(ReservationOperationStatus.InvalidInput, "reservation.requestRequired");

        Reservation candidate;
        try
        {
            // Fabryka sprawdza dane i czas przed pierwszym odczytem bazy. Obiekt nie jest jeszcze śledzony ani zapisany.
            candidate = Reservation.Create(request.RoomId, userId!.Value, request.Title,
                new ReservationPeriod(request.StartAt, request.EndAt), timeProvider);
        }
        catch (DomainException error)
        {
            return ReservationOperationResult.Failed(ReservationOperationStatus.InvalidInput, error.Code);
        }

        try
        {
            var room = await rooms.FindByIdAsync(candidate.RoomId, cancellationToken);
            if (room is null)
                return ReservationOperationResult.Failed(ReservationOperationStatus.RoomNotFound, "reservation.roomNotFound");
            if (!room.IsActive)
                return ReservationOperationResult.Failed(ReservationOperationStatus.RoomInactive, "reservation.roomInactive");
            if (await reservations.HasActiveConflictAsync(candidate.RoomId, candidate.Period, cancellationToken))
                return ReservationOperationResult.Failed(ReservationOperationStatus.Conflict, "reservation.conflict");

            // Wynik SELECT nie rezerwuje terminu: ostateczną ochroną jest constraint przy INSERT.
            var saved = await reservations.AddAsync(candidate, cancellationToken);
            return ReservationSaveResults.From(saved, candidate);
        }
        catch (ReservationStorageException)
        {
            return ReservationOperationResult.Failed(ReservationOperationStatus.InfrastructureFailure, "reservation.storageFailure");
        }
    }
}
