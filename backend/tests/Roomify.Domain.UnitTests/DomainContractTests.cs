using Roomify.Domain.Errors;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;

namespace Roomify.Domain.UnitTests;

/// <summary>Kontrakty wejścia i komunikatów, których nie obejmowały wcześniejsze przypadki graniczne.</summary>
public sealed class DomainContractTests
{
    [Theory]
    [InlineData("Room.Create", "timeProvider")]
    [InlineData("Reservation.Create.clock", "timeProvider")]
    [InlineData("Reservation.Create.period", "period")]
    [InlineData("ChangePeriod.clock", "timeProvider")]
    [InlineData("ChangePeriod.period", "period")]
    [InlineData("Cancel.active", "timeProvider")]
    [InlineData("Cancel.cancelled", "timeProvider")]
    [InlineData("Blocks.active", "period")]
    [InlineData("Blocks.cancelled", "period")]
    [InlineData("Overlaps", "other")]
    [InlineData("TimePolicy", "period")]
    public void PublicOperations_RejectNullDependenciesWithoutChangingState(string operation, string parameter)
    {
        var clock = DomainTestData.Clock();
        var reservation = DomainTestData.Reservation(clock);
        if (operation.EndsWith("cancelled", StringComparison.Ordinal)) reservation.Cancel(clock);
        var before = (reservation.Period, reservation.Status, reservation.UpdatedAt);
        Action action = operation switch
        {
            "Room.Create" => () => Room.Create("Sala", null, "Piętro 1", 2, null!),
            "Reservation.Create.clock" => () => Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), "Spotkanie", reservation.Period, null!),
            "Reservation.Create.period" => () => Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), "Spotkanie", null!, clock),
            "ChangePeriod.clock" => () => reservation.ChangePeriod(reservation.Period, null!),
            "ChangePeriod.period" => () => reservation.ChangePeriod(null!, clock),
            "Cancel.active" or "Cancel.cancelled" => () => reservation.Cancel(null!),
            "Blocks.active" or "Blocks.cancelled" => () => reservation.Blocks(null!),
            "Overlaps" => () => reservation.Period.Overlaps(null!),
            "TimePolicy" => () => new ReservationTimePolicy().Validate(null!, clock.UtcNow),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        var error = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(parameter, error.ParamName);
        Assert.Equal(before, (reservation.Period, reservation.Status, reservation.UpdatedAt));
    }

    [Theory]
    [InlineData("name", "room.name", DomainErrorKind.InvalidInput)]
    [InlineData("capacity", "room.capacity", DomainErrorKind.InvalidInput)]
    [InlineData("roomId", "reservation.roomId", DomainErrorKind.InvalidInput)]
    [InlineData("order", "BR-001", DomainErrorKind.InvalidPeriod)]
    [InlineData("future", "BR-002", DomainErrorKind.InvalidPeriod)]
    [InlineData("duration", "BR-007", DomainErrorKind.InvalidPeriod)]
    [InlineData("hours", "BR-008", DomainErrorKind.InvalidPeriod)]
    [InlineData("day", "BR-014", DomainErrorKind.InvalidPeriod)]
    [InlineData("minute", "BR-015", DomainErrorKind.InvalidPeriod)]
    [InlineData("state", "BR-017", DomainErrorKind.InvalidState)]
    public void RejectedOperation_ProvidesRuleCategoryAndNonemptyPolishExplanation(string scenario, string code, DomainErrorKind kind)
    {
        var clock = DomainTestData.Clock();
        var period = DomainTestData.Period();
        var policy = new ReservationTimePolicy();
        Action action = scenario switch
        {
            "name" => () => Room.Create(" ", null, "Piętro 1", 1, clock),
            "capacity" => () => Room.Create("Sala", null, "Piętro 1", 0, clock),
            "roomId" => () => Reservation.Create(Guid.Empty, Guid.NewGuid(), "Spotkanie", period, clock),
            "order" => () => _ = new ReservationPeriod(period.Start, period.Start),
            "future" => () => policy.Validate(period, period.Start),
            "duration" => () => policy.Validate(new ReservationPeriod(period.Start, period.Start.AddHours(8).AddMinutes(1)), clock.UtcNow),
            "hours" => () => policy.Validate(DomainTestData.Period(5, 6), clock.UtcNow),
            "day" => () => policy.Validate(new ReservationPeriod(
                DateTimeOffset.Parse("2026-06-01T21:30:00Z"), DateTimeOffset.Parse("2026-06-01T22:30:00Z")), clock.UtcNow),
            "minute" => () => policy.Validate(new ReservationPeriod(period.Start.AddTicks(1), period.End), clock.UtcNow),
            "state" => () =>
            {
                var reservation = DomainTestData.Reservation(clock);
                reservation.Cancel(clock);
                reservation.ChangePeriod(period, clock);
            },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        var error = Assert.Throws<DomainException>(action);
        Assert.Equal(code, error.Code);
        Assert.Equal(kind, error.Kind);
        // Treść nie jest kontraktem HTTP, ale pusta informacja o przyczynie jest nieużyteczna.
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Theory]
    [InlineData("2026-03-29", 6)]
    [InlineData("2026-10-25", 7)]
    public void StandaloneTimePolicy_UsesWarsawOnBothDstTransitionDates(string date, int openingUtcHour)
    {
        var policy = new ReservationTimePolicy();
        var opening = DateTimeOffset.Parse(date + "T00:00:00Z").AddHours(openingUtcHour);
        var clock = DomainTestData.Clock();
        policy.Validate(new ReservationPeriod(opening, opening.AddHours(8)), clock.UtcNow);
        var error = Assert.Throws<DomainException>(() => policy.Validate(
            new ReservationPeriod(opening.AddMinutes(-1), opening.AddHours(1)), clock.UtcNow));
        Assert.Equal("BR-008", error.Code);
    }
}
