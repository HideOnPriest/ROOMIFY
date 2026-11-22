using Roomify.Domain.Reservations;

namespace Roomify.Domain.UnitTests;

/// <summary>Zegar należy do pojedynczego testu; żadnego współdzielonego stanu ani DateTime.Now.</summary>
internal sealed class ControlledTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal static class DomainTestData
{
    internal static ControlledTimeProvider Clock() => new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    internal static ReservationPeriod Period(int startHour = 9, int endHour = 10) => new(
        new DateTimeOffset(2026, 1, 15, startHour, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 15, endHour, 0, 0, TimeSpan.Zero));

    internal static Reservation Reservation(ControlledTimeProvider? clock = null) => Reservations.Reservation.Create(
        Guid.NewGuid(), Guid.NewGuid(), "Spotkanie", Period(), clock ?? Clock());
}
