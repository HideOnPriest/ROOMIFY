using Roomify.Application.Abstractions;
using Roomify.Domain.Errors;
using Roomify.Domain.Reservations;

namespace Roomify.Application.Reservations;

public sealed class CancelReservationService(IReservationRepository reservations, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<ReservationOperationResult> CancelAsync(CancelReservationRequest? request, CancellationToken cancellationToken = default)
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
            // Powtórzenie jest bez zapisu i bez zmiany UpdatedAt, także po pierwotnym początku.
            if (reservation.Status == ReservationStatus.Cancelled)
                return ReservationOperationResult.Failed(ReservationOperationStatus.AlreadyCancelled, "reservation.alreadyCancelled");
            reservation.Cancel(timeProvider);
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
