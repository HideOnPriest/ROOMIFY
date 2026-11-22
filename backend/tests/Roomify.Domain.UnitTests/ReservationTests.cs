using Roomify.Domain.Errors;
using Roomify.Domain.Reservations;

namespace Roomify.Domain.UnitTests;

[Trait("Rules", "BR-009, BR-012, BR-017, BR-018, BR-023, BR-024, BR-027")]
public sealed class ReservationTests
{
    [Fact]
    public void PeriodSnapshot_RemainsImmutableWhenReservationChangesItsScalarEndpoints()
    {
        var clock = DomainTestData.Clock();
        var reservation = DomainTestData.Reservation(clock);
        var snapshot = reservation.Period;
        var next = DomainTestData.Period(11, 12);

        reservation.ChangePeriod(next, clock);

        Assert.Equal(DomainTestData.Period(), snapshot);
        Assert.Equal(next.Start, reservation.StartAt);
        Assert.Equal(next.End, reservation.EndAt);
        Assert.Equal(next, reservation.Period);
    }

    [Fact]
    public void Create_StoresUtcEndpointsFromPeriodWithNonZeroInputOffsets()
    {
        var start = new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.FromHours(2));
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), "Spotkanie",
            new ReservationPeriod(start, start.AddHours(1)), DomainTestData.Clock());
        Assert.Equal(TimeSpan.Zero, reservation.StartAt.Offset);
        Assert.Equal(TimeSpan.Zero, reservation.EndAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 6, 0, 0, TimeSpan.Zero), reservation.StartAt);
        Assert.Equal(reservation.StartAt, reservation.Period.Start);
        Assert.Equal(reservation.EndAt, reservation.Period.End);
    }

    [Fact]
    public void Create_PreservesIdentifiersAndStoresValidActiveReservation()
    {
        var clock = DomainTestData.Clock();
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var period = DomainTestData.Period();
        var reservation = Reservation.Create(roomId, userId, "  Planowanie  ", period, clock);

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(roomId, reservation.RoomId);
        Assert.Equal(userId, reservation.UserId);
        Assert.Equal("Planowanie", reservation.Title);
        Assert.Equal(period, reservation.Period);
        Assert.Equal(period.Start, reservation.StartAt);
        Assert.Equal(period.End, reservation.EndAt);
        Assert.Equal(ReservationStatus.Active, reservation.Status);
        Assert.Equal(clock.UtcNow, reservation.CreatedAt);
        Assert.Null(reservation.UpdatedAt);
        Assert.True(reservation.Blocks(period));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void Create_RejectsMissingTitle(string? title)
    {
        var error = Assert.Throws<DomainException>(() => Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), title,
            DomainTestData.Period(), DomainTestData.Clock()));
        Assert.Equal(DomainErrorKind.InvalidInput, error.Kind);
        Assert.Equal("BR-024", error.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    public void Create_AcceptsTitleLengthBoundariesAfterTrim(int length)
    {
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), " " + new string('T', length) + " ",
            DomainTestData.Period(), DomainTestData.Clock());
        Assert.Equal(new string('T', length), reservation.Title);
    }

    [Fact]
    public void Create_RejectsTitleLongerThan120Characters()
    {
        var error = Assert.Throws<DomainException>(() => Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), new string('T', 121),
            DomainTestData.Period(), DomainTestData.Clock()));
        Assert.Equal("BR-024", error.Code);
    }

    [Theory]
    [InlineData(true, "reservation.roomId")]
    [InlineData(false, "reservation.userId")]
    public void Create_RejectsEmptyIdentifiers(bool emptyRoom, string code)
    {
        var error = Assert.Throws<DomainException>(() => Reservation.Create(emptyRoom ? Guid.Empty : Guid.NewGuid(),
            emptyRoom ? Guid.NewGuid() : Guid.Empty, "Spotkanie", DomainTestData.Period(), DomainTestData.Clock()));
        Assert.Equal(DomainErrorKind.InvalidInput, error.Kind);
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void ChangePeriod_UpdatesTermAndTimestampWithoutChangingOwnerOrIdentity()
    {
        var clock = DomainTestData.Clock();
        var reservation = DomainTestData.Reservation(clock);
        var original = (reservation.Id, reservation.RoomId, reservation.UserId, reservation.Title, reservation.CreatedAt);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var next = DomainTestData.Period(10, 12);

        reservation.ChangePeriod(next, clock);

        Assert.Equal(next, reservation.Period);
        Assert.Equal(next.Start, reservation.StartAt);
        Assert.Equal(next.End, reservation.EndAt);
        Assert.Equal(clock.UtcNow, reservation.UpdatedAt);
        Assert.Equal(original, (reservation.Id, reservation.RoomId, reservation.UserId, reservation.Title, reservation.CreatedAt));
        Assert.Equal(ReservationStatus.Active, reservation.Status);
    }

    [Theory]
    [InlineData("2026-01-01T00:00:00Z", "2026-01-01T01:00:00Z", "BR-002")]
    [InlineData("2026-01-15T06:00:00Z", "2026-01-15T07:00:00Z", "BR-008")]
    [InlineData("2026-01-15T07:00:00Z", "2026-01-15T15:01:00Z", "BR-007")]
    [InlineData("2026-01-15T09:00:01Z", "2026-01-15T10:00:00Z", "BR-015")]
    [InlineData("2026-01-15T22:30:00Z", "2026-01-15T23:30:00Z", "BR-014")]
    public void ChangePeriod_RevalidatesRulesAndKeepsOldDataOnFailure(string start, string end, string code)
    {
        var clock = DomainTestData.Clock();
        var reservation = DomainTestData.Reservation(clock);
        var old = reservation.Period;
        var error = Assert.Throws<DomainException>(() => reservation.ChangePeriod(
            new ReservationPeriod(DateTimeOffset.Parse(start), DateTimeOffset.Parse(end)), clock));
        Assert.Equal(DomainErrorKind.InvalidPeriod, error.Kind);
        Assert.Equal(code, error.Code);
        Assert.Equal(old, reservation.Period);
        Assert.Null(reservation.UpdatedAt);
        Assert.Equal(ReservationStatus.Active, reservation.Status);
    }

    [Fact]
    public void Cancel_ChangesStatusAndFreesTermWithoutDeletingReservation()
    {
        var clock = DomainTestData.Clock();
        var reservation = DomainTestData.Reservation(clock);
        var data = (reservation.Id, reservation.RoomId, reservation.UserId, reservation.Title, reservation.Period, reservation.CreatedAt);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        reservation.Cancel(clock);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(clock.UtcNow, reservation.UpdatedAt);
        Assert.False(reservation.Blocks(reservation.Period));
        Assert.Equal(data, (reservation.Id, reservation.RoomId, reservation.UserId, reservation.Title, reservation.Period, reservation.CreatedAt));
        // Nowa rezerwacja tego terminu jest domenowo poprawna; baza będzie osobnym etapem.
        var replacement = Reservation.Create(reservation.RoomId, reservation.UserId, "Nowe spotkanie", reservation.Period, clock);
        Assert.True(replacement.Blocks(replacement.Period));
    }

    [Fact]
    public void Cancel_RepeatedCallIsIdempotentEvenAfterOriginalStart()
    {
        var clock = DomainTestData.Clock();
        var reservation = DomainTestData.Reservation(clock);
        reservation.Cancel(clock);
        var updated = reservation.UpdatedAt;
        clock.UtcNow = reservation.EndAt.AddDays(1);
        reservation.Cancel(clock);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(updated, reservation.UpdatedAt);
    }

    [Fact]
    public void ChangePeriod_RejectsCancelledReservationWithoutChangingMetadata()
    {
        var clock = DomainTestData.Clock();
        var reservation = DomainTestData.Reservation(clock);
        reservation.Cancel(clock);
        var old = reservation.Period;
        var updated = reservation.UpdatedAt;
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var error = Assert.Throws<DomainException>(() => reservation.ChangePeriod(DomainTestData.Period(10, 11), clock));
        Assert.Equal(DomainErrorKind.InvalidState, error.Kind);
        Assert.Equal("BR-017", error.Code);
        Assert.Equal(old, reservation.Period);
        Assert.Equal(updated, reservation.UpdatedAt);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void ChangeAndCancel_RequireOriginalReservationNotStarted(int nowRelativeTicks, bool allowed)
    {
        var clock = DomainTestData.Clock();
        var toCancel = DomainTestData.Reservation(clock);
        var toChange = DomainTestData.Reservation(clock);
        clock.UtcNow = toCancel.StartAt.AddTicks(nowRelativeTicks);
        if (allowed)
        {
            toCancel.Cancel(clock);
            toChange.ChangePeriod(DomainTestData.Period(10, 11), clock);
            Assert.Equal(ReservationStatus.Cancelled, toCancel.Status);
            Assert.Equal(DomainTestData.Period(10, 11), toChange.Period);
        }
        else
        {
            Assert.Equal(DomainErrorKind.InvalidState, Assert.Throws<DomainException>(() => toCancel.Cancel(clock)).Kind);
            Assert.Equal(DomainErrorKind.InvalidState, Assert.Throws<DomainException>(() => toChange.ChangePeriod(DomainTestData.Period(10, 11), clock)).Kind);
            Assert.Null(toCancel.UpdatedAt);
            Assert.Null(toChange.UpdatedAt);
            Assert.Equal(ReservationStatus.Active, toCancel.Status);
        }
    }

    [Fact]
    public void Blocks_ActiveReservationDoesNotBlockAdjacentOrSeparateTerms()
    {
        var reservation = DomainTestData.Reservation();
        Assert.False(reservation.Blocks(DomainTestData.Period(10, 11)));
        Assert.False(reservation.Blocks(DomainTestData.Period(11, 12)));
        Assert.True(reservation.Blocks(new ReservationPeriod(reservation.StartAt.AddMinutes(30), reservation.EndAt.AddMinutes(30))));
    }
}
