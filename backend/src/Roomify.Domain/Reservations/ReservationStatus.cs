namespace Roomify.Domain.Reservations;

/// <summary>Minimalny cykl życia z WP-00: Active → Cancelled.</summary>
public enum ReservationStatus
{
    Active,
    Cancelled,
}
