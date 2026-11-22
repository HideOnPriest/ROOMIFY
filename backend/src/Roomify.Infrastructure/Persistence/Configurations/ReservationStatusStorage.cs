using Roomify.Domain.Reservations;

namespace Roomify.Infrastructure.Persistence.Configurations;

internal static class ReservationStatusStorage
{
    internal static string ToStorage(ReservationStatus status) => status switch
    {
        ReservationStatus.Active => "active",
        ReservationStatus.Cancelled => "cancelled",
        _ => throw new InvalidOperationException("Nieobsługiwany status rezerwacji do zapisu."),
    };

    internal static ReservationStatus FromStorage(string status) => status switch
    {
        "active" => ReservationStatus.Active,
        "cancelled" => ReservationStatus.Cancelled,
        _ => throw new InvalidOperationException("Nieznany status rezerwacji w danych."),
    };
}
