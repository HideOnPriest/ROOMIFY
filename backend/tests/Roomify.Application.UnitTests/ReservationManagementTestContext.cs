using Moq;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;

namespace Roomify.Application.UnitTests;

internal sealed class ReservationManagementTestContext
{
    internal Mock<IReservationRepository> Reservations { get; } = new(MockBehavior.Strict);
    internal Mock<IRoomRepository> Rooms { get; } = new(MockBehavior.Strict);
    internal Mock<ICurrentUser> User { get; } = new(MockBehavior.Strict);
    internal ManagementClock Clock { get; } = new();
    internal Room Room { get; }
    internal Reservation Reservation { get; }
    internal UpdateReservationRequest Update { get; }
    internal UpdateReservationService UpdateService { get; }
    internal CancelReservationService CancelService { get; }

    internal ReservationManagementTestContext()
    {
        Room = Room.Create("Sala", null, "Piętro 1", 12, Clock);
        Reservation = Reservation.Create(Room.Id, Guid.NewGuid(), "Pierwotny tytuł", new(
            new(2030, 1, 15, 9, 0, 0, TimeSpan.Zero), new(2030, 1, 15, 10, 0, 0, TimeSpan.Zero)), Clock);
        Update = new(Reservation.Id, "  Nowy tytuł  ", Reservation.StartAt.AddHours(1), Reservation.EndAt.AddHours(1));
        User.SetupGet(x => x.UserId).Returns(Reservation.UserId);
        User.SetupGet(x => x.IsActive).Returns(true);
        User.SetupGet(x => x.IsAdministrator).Returns(false);
        Reservations.Setup(x => x.FindByIdAsync(Reservation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);
        Reservations.Setup(x => x.HasActiveConflictExcludingAsync(Room.Id, It.IsAny<ReservationPeriod>(), Reservation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Reservations.Setup(x => x.SaveAsync(Reservation, It.IsAny<CancellationToken>())).ReturnsAsync(ReservationWriteOutcome.Saved);
        Rooms.Setup(x => x.FindByIdAsync(Room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Room);
        UpdateService = new(Reservations.Object, Rooms.Object, User.Object, Clock);
        CancelService = new(Reservations.Object, User.Object, Clock);
    }

    internal static void AssertFailure(ReservationOperationResult result, ReservationOperationStatus status, string code)
    {
        Assert.Equal(status, result.Status);
        Assert.Equal(code, result.ErrorCode);
        Assert.Null(result.Reservation);
    }
}

internal sealed class ManagementClock : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
