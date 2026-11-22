using Roomify.Domain.Errors;
using Roomify.Domain.Reservations;

namespace Roomify.Domain.UnitTests;

[Trait("Rules", "BR-002, BR-007, BR-008, BR-014, BR-015, BR-016")]
public sealed class ReservationTimePolicyTests
{
    [Theory]
    [InlineData("2026-01-15T07:00:00Z", "2026-01-15T08:00:00Z")]
    [InlineData("2026-01-15T18:30:00Z", "2026-01-15T19:00:00Z")]
    [InlineData("2026-01-15T07:00:00Z", "2026-01-15T15:00:00Z")]
    [InlineData("2026-01-15T11:00:00Z", "2026-01-15T19:00:00Z")]
    [InlineData("2026-06-15T06:00:00Z", "2026-06-15T14:00:00Z")]
    public void Booking_AcceptsOpeningClosingAndEightHourBoundaries(string start, string end)
    {
        var reservation = Create(start, end);
        Assert.Equal(DateTimeOffset.Parse(start), reservation.StartAt);
        Assert.Equal(DateTimeOffset.Parse(end), reservation.EndAt);
        Assert.Equal(ReservationStatus.Active, reservation.Status);
    }

    [Theory]
    [InlineData("2026-01-15T06:59:00Z", "2026-01-15T08:00:00Z", "BR-008")]
    [InlineData("2026-01-15T18:00:00Z", "2026-01-15T19:01:00Z", "BR-008")]
    [InlineData("2026-01-15T19:00:00Z", "2026-01-15T19:30:00Z", "BR-008")]
    [InlineData("2026-01-15T07:00:00Z", "2026-01-15T15:01:00Z", "BR-007")]
    [InlineData("2026-01-15T07:00:00Z", "2026-01-15T15:00:00.0000001Z", "BR-007")]
    [InlineData("2026-01-15T09:00:00Z", "2026-01-15T09:00:01Z", "BR-015")]
    [InlineData("2026-01-15T09:00:00.0000001Z", "2026-01-15T10:00:00Z", "BR-015")]
    [InlineData("2026-01-15T09:00:01Z", "2026-01-15T10:00:00Z", "BR-015")]
    [InlineData("2026-01-15T09:00:00Z", "2026-01-15T10:00:00.0000001Z", "BR-015")]
    [InlineData("2026-01-15T22:30:00Z", "2026-01-15T23:30:00Z", "BR-014")]
    [InlineData("2026-01-15T23:30:00Z", "2026-01-16T00:30:00Z", "BR-008")]
    public void Booking_RejectsInvalidBusinessPeriod(string start, string end, string code)
    {
        var error = Assert.Throws<DomainException>(() => Create(start, end));
        Assert.Equal(DomainErrorKind.InvalidPeriod, error.Kind);
        Assert.Equal(code, error.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Booking_UsesInjectedClockAndRequiresStrictlyFutureStart(int nowRelativeTicks)
    {
        var period = DomainTestData.Period();
        var clock = new ControlledTimeProvider(period.Start.AddTicks(nowRelativeTicks));
        if (nowRelativeTicks < 0)
        {
            var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), "Spotkanie", period, clock);
            Assert.Equal(clock.UtcNow, reservation.CreatedAt);
        }
        else
        {
            var error = Assert.Throws<DomainException>(() => Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), "Spotkanie", period, clock));
            Assert.Equal("BR-002", error.Code);
        }
    }

    // Dane oczekiwane są jawne: o 08:00 w Warszawie jest 07:00Z zimą i 06:00Z latem.
    [Theory]
    [InlineData("2026-03-28", 7, 19)]
    [InlineData("2026-03-29", 6, 18)]
    [InlineData("2026-10-24", 6, 18)]
    [InlineData("2026-10-25", 7, 19)]
    public void Booking_RespectsWarsawOffsetsBeforeAndAfterBothDstChanges(string date, int openingUtcHour, int closingUtcHour)
    {
        var midnight = DateTimeOffset.Parse(date + "T00:00:00Z");
        var opening = midnight.AddHours(openingUtcHour);
        var closing = midnight.AddHours(closingUtcHour);
        var clock = DomainTestData.Clock();

        Reservation CreateAt(DateTimeOffset start, DateTimeOffset end) => Reservation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Spotkanie", new ReservationPeriod(start, end), clock);

        Assert.Equal(opening, CreateAt(opening, opening.AddHours(1)).StartAt);
        Assert.Equal(closing, CreateAt(closing.AddHours(-1), closing).EndAt);
        Assert.Equal("BR-008", Assert.Throws<DomainException>(() => CreateAt(opening.AddMinutes(-1), opening.AddHours(1))).Code);
        Assert.Equal("BR-008", Assert.Throws<DomainException>(() => CreateAt(closing.AddHours(-1), closing.AddMinutes(1))).Code);
    }

    [Fact]
    public void Policy_RejectsMultiDayPeriodEvenWhenUtcDatesDifferFromWarsaw()
    {
        // Oba końce mają tę samą datę UTC, ale różne daty lokalne.
        var error = Assert.Throws<DomainException>(() => Create("2026-06-01T21:30:00Z", "2026-06-01T22:30:00Z"));
        Assert.Equal("BR-014", error.Code);
    }

    private static Reservation Create(string start, string end) => Reservation.Create(Guid.NewGuid(), Guid.NewGuid(),
        "Spotkanie", new ReservationPeriod(DateTimeOffset.Parse(start), DateTimeOffset.Parse(end)), DomainTestData.Clock());
}
