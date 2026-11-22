using Roomify.Domain.Errors;
using Roomify.Domain.Reservations;

namespace Roomify.Domain.UnitTests;

[Trait("Rules", "BR-001, BR-003, BR-004")]
public sealed class ReservationPeriodTests
{
    [Fact]
    public void Constructor_NormalizesOffsetsAndCalculatesActualDuration()
    {
        var period = new ReservationPeriod(
            new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.FromHours(1)));
        Assert.Equal(TimeSpan.Zero, period.Start.Offset);
        Assert.Equal(TimeSpan.Zero, period.End.Offset);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), period.Start);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero), period.End);
        Assert.Equal(TimeSpan.FromMinutes(90), period.Duration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsEqualOrEarlierEnd(int minutes)
    {
        var start = DomainTestData.Period().Start;
        var error = Assert.Throws<DomainException>(() => new ReservationPeriod(start, start.AddMinutes(minutes)));
        Assert.Equal(DomainErrorKind.InvalidPeriod, error.Kind);
        Assert.Equal("BR-001", error.Code);
    }

    [Fact]
    public void Constructor_OrdersInstantsInsteadOfDisplayedLocalTimes()
    {
        var start = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(1));
        var end = new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.FromHours(2));
        Assert.Throws<DomainException>(() => new ReservationPeriod(start, end));
    }

    [Theory]
    [InlineData(-60, 30, true)]
    [InlineData(30, 120, true)]
    [InlineData(-60, 120, true)]
    [InlineData(15, 45, true)]
    [InlineData(0, 60, true)]
    [InlineData(-60, 60, true)]
    [InlineData(0, 120, true)]
    [InlineData(-60, 0, false)]
    [InlineData(60, 120, false)]
    [InlineData(-120, -60, false)]
    [InlineData(120, 180, false)]
    public void Overlaps_UsesHalfOpenIntervalsSymmetrically(int startMinutes, int endMinutes, bool expected)
    {
        var period = DomainTestData.Period();
        var other = new ReservationPeriod(period.Start.AddMinutes(startMinutes), period.Start.AddMinutes(endMinutes));
        Assert.Equal(expected, period.Overlaps(other));
        Assert.Equal(expected, other.Overlaps(period));
    }

    [Fact]
    public void Period_CanCrossUtcMidnightWithoutInventingBookingPolicy()
    {
        var period = new ReservationPeriod(new DateTimeOffset(2026, 1, 1, 23, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 0, 30, 0, TimeSpan.Zero));
        var next = new ReservationPeriod(period.End, period.End.AddHours(1));
        Assert.Equal(TimeSpan.FromHours(1), period.Duration);
        Assert.False(period.Overlaps(next));
    }

    [Fact]
    public void Equality_UsesNormalizedValueAndCopyCannotChangeEndpoints()
    {
        var utc = DomainTestData.Period();
        var offset = new ReservationPeriod(utc.Start.ToOffset(TimeSpan.FromHours(2)), utc.End.ToOffset(TimeSpan.FromHours(2)));
        Assert.Equal(utc, offset);
        Assert.Equal(utc.GetHashCode(), offset.GetHashCode());
        Assert.Equal(utc, utc with { });
        Assert.NotEqual(utc, new ReservationPeriod(utc.Start, utc.End.AddTicks(1)));
        Assert.NotEqual(utc, new ReservationPeriod(utc.Start.AddTicks(1), utc.End));
    }
}
