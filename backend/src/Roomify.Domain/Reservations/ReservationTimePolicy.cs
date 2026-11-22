using Roomify.Domain.Errors;

namespace Roomify.Domain.Reservations;

/// <summary>
/// Polityka czasu Roomify. Godziny są lokalne dla Europe/Warsaw, a czas trwania
/// wynika z rzeczywistych chwil UTC. Nie korzystamy ze strefy lokalnej hosta.
/// </summary>
public sealed class ReservationTimePolicy
{
    private readonly TimeZoneInfo _businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public void Validate(ReservationPeriod period, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(period);
        if (period.Start <= now)
        {
            throw new DomainException(DomainErrorKind.InvalidPeriod, "BR-002", "Początek musi być późniejszy od bieżącej chwili.");
        }

        if (period.Duration > TimeSpan.FromHours(8))
        {
            throw new DomainException(DomainErrorKind.InvalidPeriod, "BR-007", "Rezerwacja może trwać najwyżej 8 godzin.");
        }

        if (period.Start.Ticks % TimeSpan.TicksPerMinute != 0 || period.End.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw new DomainException(DomainErrorKind.InvalidPeriod, "BR-015", "Termin wymaga dokładności jednej minuty.");
        }

        var localStart = TimeZoneInfo.ConvertTime(period.Start, _businessTimeZone);
        var localEnd = TimeZoneInfo.ConvertTime(period.End, _businessTimeZone);
        if (localStart.Date != localEnd.Date)
        {
            throw new DomainException(DomainErrorKind.InvalidPeriod, "BR-014", "Termin musi mieścić się w jednym lokalnym dniu.");
        }

        var start = TimeOnly.FromDateTime(localStart.DateTime);
        var end = TimeOnly.FromDateTime(localEnd.DateTime);
        if (start < new TimeOnly(8, 0) || end > new TimeOnly(20, 0))
        {
            throw new DomainException(DomainErrorKind.InvalidPeriod, "BR-008", "Termin musi mieścić się w godzinach 08:00–20:00 Europe/Warsaw.");
        }
    }
}
