using Roomify.Domain.Errors;

namespace Roomify.Domain.Reservations;

/// <summary>
/// Niemutowalny przedział półotwarty [Start, End). Opisuje chwile w UTC,
/// bez zależności od zegara i godzin dostępności sal.
/// </summary>
public sealed record ReservationPeriod
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }
    public TimeSpan Duration => End - Start;

    public ReservationPeriod(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new DomainException(DomainErrorKind.InvalidPeriod, "BR-001", "Koniec musi być późniejszy niż początek.");
        }

        Start = start.ToUniversalTime();
        End = end.ToUniversalTime();
    }

    public bool Overlaps(ReservationPeriod other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && End > other.Start;
    }
}
