using Roomify.Application.Abstractions;
using Roomify.Domain.Errors;
using Roomify.Domain.Reservations;

namespace Roomify.Application.Reservations;

public sealed class UpdateReservationService(IReservationRepository reservations, IRoomRepository rooms,
    ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<ReservationOperationResult> UpdateAsync(UpdateReservationRequest? request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = currentUser.UserId;
        if (ApplicationAccess.Check(currentUser) is { } denied)
            return ReservationOperationResult.Failed(denied == AccessFailure.Unauthenticated
                ? ReservationOperationStatus.Unauthenticated : ReservationOperationStatus.Forbidden,
                denied == AccessFailure.Unauthenticated ? "reservation.unauthenticated" : "reservation.forbidden");
        if (request is null || request.ReservationId == Guid.Empty)
            return ReservationOperationResult.Failed(ReservationOperationStatus.InvalidInput, "reservation.idRequired");
        try
        {
            var reservation = await reservations.FindByIdAsync(request.ReservationId, cancellationToken);
            if (reservation is null)
                return ReservationOperationResult.Failed(ReservationOperationStatus.ReservationNotFound, "reservation.notFound");
            if (!ReservationAccess.CanManage(reservation, userId!.Value, currentUser))
                return ReservationOperationResult.Failed(ReservationOperationStatus.Forbidden, "reservation.forbidden");
            if (reservation.Status == ReservationStatus.Cancelled)
                return ReservationOperationResult.Failed(ReservationOperationStatus.AlreadyCancelled, "reservation.alreadyCancelled");

            var period = new ReservationPeriod(request.StartAt, request.EndAt);
            // Walidacja bez mutacji: odrzucony precheck nie zostawia zmienionej encji w trackerze.
            reservation.ValidateUpdate(request.Title, period, timeProvider);
            var room = await rooms.FindByIdAsync(reservation.RoomId, cancellationToken);
            if (room is null)
                return ReservationOperationResult.Failed(ReservationOperationStatus.RoomNotFound, "reservation.roomNotFound");
            if (!room.IsActive)
                return ReservationOperationResult.Failed(ReservationOperationStatus.RoomInactive, "reservation.roomInactive");
            if (await reservations.HasActiveConflictExcludingAsync(reservation.RoomId, period, reservation.Id, cancellationToken))
                return ReservationOperationResult.Failed(ReservationOperationStatus.Conflict, "reservation.conflict");

            // Domena ponownie odczytuje zegar przy właściwej zmianie, nie kopiuje walidacji do Application.
            reservation.UpdateDetails(request.Title, period, timeProvider);
            return ReservationSaveResults.From(await reservations.SaveAsync(reservation, cancellationToken), reservation);
        }
        catch (DomainException error)
        {
            return ReservationOperationResult.Failed(ReservationOperationStatus.InvalidInput, error.Code);
        }
        catch (ReservationStorageException)
        {
            return ReservationOperationResult.Failed(ReservationOperationStatus.InfrastructureFailure, "reservation.storageFailure");
        }
    }
}
