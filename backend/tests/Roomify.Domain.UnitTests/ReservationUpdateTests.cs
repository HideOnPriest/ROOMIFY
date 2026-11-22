using Roomify.Domain.Errors;

namespace Roomify.Domain.UnitTests;

public sealed class ReservationUpdateTests
{
    [Fact]
    public void UpdateDetails_ChangesTitleAndPeriodAtomicallyWithoutChangingIdentity()
    {
        var clock = DomainTestData.Clock();
        var reservation = DomainTestData.Reservation(clock);
        var before = (reservation.Id, reservation.RoomId, reservation.UserId, reservation.CreatedAt);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var period = DomainTestData.Period(11, 12);
        reservation.UpdateDetails("  Zmieniony tytuł  ", period, clock);
        Assert.Equal("Zmieniony tytuł", reservation.Title);
        Assert.Equal(period, reservation.Period);
        Assert.Equal(clock.UtcNow, reservation.UpdatedAt);
        Assert.Equal(before, (reservation.Id, reservation.RoomId, reservation.UserId, reservation.CreatedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void UpdateDetails_InvalidTitleNeverPartiallyChangesPeriod(string? title)
    {
        var reservation = DomainTestData.Reservation();
        var before = (reservation.Title, reservation.Period, reservation.UpdatedAt);
        Assert.Equal("BR-024", Assert.Throws<DomainException>(() => reservation.UpdateDetails(title, DomainTestData.Period(11, 12), DomainTestData.Clock())).Code);
        Assert.Equal(before, (reservation.Title, reservation.Period, reservation.UpdatedAt));
    }

    [Fact]
    public void UpdateDetails_InvalidPeriodNeverPartiallyChangesTitle()
    {
        var reservation = DomainTestData.Reservation();
        var before = (reservation.Title, reservation.Period, reservation.UpdatedAt);
        Assert.Equal("BR-008", Assert.Throws<DomainException>(() => reservation.UpdateDetails("Nowy", DomainTestData.Period(5, 6), DomainTestData.Clock())).Code);
        Assert.Equal(before, (reservation.Title, reservation.Period, reservation.UpdatedAt));
    }

    [Fact]
    public void ValidateUpdate_IsPureAndDoesNotChangeMetadata()
    {
        var reservation = DomainTestData.Reservation();
        var before = (reservation.Title, reservation.Period, reservation.UpdatedAt);
        reservation.ValidateUpdate("Nowy", DomainTestData.Period(11, 12), DomainTestData.Clock());
        Assert.Equal(before, (reservation.Title, reservation.Period, reservation.UpdatedAt));
    }
}
