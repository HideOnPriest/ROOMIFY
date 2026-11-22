using Roomify.Application.Abstractions;
using Roomify.Domain.Reservations;

namespace Roomify.Application.Reservations;

internal static class ReservationAccess
{
    internal static bool CanManage(Reservation reservation, Guid userId, ICurrentUser user)
        => reservation.UserId == userId || user.IsAdministrator;
}
