using Moq;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;

namespace Roomify.Application.UnitTests;

public sealed class CreateReservationServiceTests
{
    [Fact]
    public async Task Create_ValidRequestSavesExactlyOnceWithTrustedOwnerAndUtcData()
    {
        // Arrange: repozytoria są mockami, ale walidację wykonuje rzeczywista domena.
        var setup = new Setup();
        using var cancellation = new CancellationTokenSource();
        Reservation? saved = null;
        setup.Reservations.Setup(x => x.AddAsync(It.IsAny<Reservation>(), cancellation.Token))
            .Callback<Reservation, CancellationToken>((reservation, _) => saved = reservation)
            .ReturnsAsync(ReservationWriteOutcome.Saved);
        var request = setup.Request with { Title = "  Planowanie  ",
            StartAt = setup.Request.StartAt.ToOffset(TimeSpan.FromHours(1)),
            EndAt = setup.Request.EndAt.ToOffset(TimeSpan.FromHours(1)) };

        // Act
        var result = await setup.Service.CreateAsync(request, cancellation.Token);

        // Assert: identyfikator użytkownika pochodzi z ICurrentUser, nie z żądania.
        Assert.Equal(ReservationOperationStatus.Success, result.Status);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(saved);
        Assert.NotNull(result.Reservation);
        Assert.Equal(setup.UserId, saved.UserId);
        Assert.Equal(setup.Room.Id, saved.RoomId);
        Assert.Equal("Planowanie", saved.Title);
        Assert.Equal(ReservationStatus.Active, saved.Status);
        Assert.Equal(setup.Clock.GetUtcNow(), saved.CreatedAt);
        Assert.Equal(TimeSpan.Zero, saved.StartAt.Offset);
        Assert.Equal(saved.Id, result.Reservation.Id);
        Assert.Equal(saved.UserId, result.Reservation.UserId);
        Assert.Equal(saved.RoomId, result.Reservation.RoomId);
        Assert.Equal(saved.Title, result.Reservation.Title);
        Assert.Equal(saved.StartAt, result.Reservation.StartAt);
        Assert.Equal(saved.EndAt, result.Reservation.EndAt);
        Assert.Equal(saved.Status, result.Reservation.Status);
        Assert.Equal(saved.CreatedAt, result.Reservation.CreatedAt);
        setup.Rooms.Verify(x => x.FindByIdAsync(setup.Room.Id, cancellation.Token), Times.Once);
        setup.Reservations.Verify(x => x.HasActiveConflictAsync(setup.Room.Id,
            new ReservationPeriod(setup.Request.StartAt, setup.Request.EndAt), cancellation.Token), Times.Once);
        setup.Reservations.Verify(x => x.AddAsync(It.IsAny<Reservation>(), cancellation.Token), Times.Once);
        setup.Rooms.VerifyNoOtherCalls();
        setup.Reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("request", "reservation.requestRequired")]
    [InlineData("roomId", "reservation.roomId")]
    [InlineData("title-null", "BR-024")]
    [InlineData("title-blank", "BR-024")]
    [InlineData("title-long", "BR-024")]
    [InlineData("equal", "BR-001")]
    [InlineData("reversed", "BR-001")]
    [InlineData("past", "BR-002")]
    [InlineData("now", "BR-002")]
    [InlineData("duration", "BR-007")]
    [InlineData("opening", "BR-008")]
    [InlineData("closing", "BR-008")]
    [InlineData("minute", "BR-015")]
    [InlineData("day", "BR-014")]
    public async Task Create_InvalidInputReturnsRuleAndDoesNotAccessRepositories(string scenario, string code)
    {
        var setup = new Setup();
        var request = scenario switch
        {
            "request" => null,
            "roomId" => setup.Request with { RoomId = Guid.Empty },
            "title-null" => setup.Request with { Title = null },
            "title-blank" => setup.Request with { Title = " \t\n" },
            "title-long" => setup.Request with { Title = new string('T', 121) },
            "equal" => setup.Request with { EndAt = setup.Request.StartAt },
            "reversed" => setup.Request with { EndAt = setup.Request.StartAt.AddTicks(-1) },
            "past" => setup.Request with { StartAt = setup.Clock.GetUtcNow().AddHours(-1), EndAt = setup.Clock.GetUtcNow() },
            "now" => setup.Request with { StartAt = setup.Clock.GetUtcNow(), EndAt = setup.Clock.GetUtcNow().AddHours(1) },
            "duration" => setup.Request with { EndAt = setup.Request.StartAt.AddHours(8).AddMinutes(1) },
            "opening" => setup.Request with { StartAt = setup.Request.StartAt.AddHours(-3) },
            "closing" => setup.Request with { StartAt = setup.Request.StartAt.AddHours(9), EndAt = setup.Request.StartAt.AddHours(10).AddMinutes(1) },
            "minute" => setup.Request with { StartAt = setup.Request.StartAt.AddTicks(1) },
            "day" => setup.Request with { StartAt = setup.Request.StartAt.AddHours(13).AddMinutes(30), EndAt = setup.Request.StartAt.AddHours(14).AddMinutes(30) },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        AssertFailure(await setup.Service.CreateAsync(request), ReservationOperationStatus.InvalidInput, code);
        setup.Rooms.VerifyNoOtherCalls();
        setup.Reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_AbsentOrEmptyIdentityIsUnauthenticatedWithoutReadingStorage(bool empty)
    {
        var setup = new Setup();
        setup.User.SetupGet(x => x.UserId).Returns(empty ? Guid.Empty : null);
        AssertFailure(await setup.Service.CreateAsync(setup.Request), ReservationOperationStatus.Unauthenticated, "reservation.unauthenticated");
        setup.Rooms.VerifyNoOtherCalls();
        setup.Reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_MissingOrArchivedRoomDoesNotCheckAvailabilityOrSave(bool archived)
    {
        var setup = new Setup();
        if (archived) setup.Room.Archive();
        else setup.Rooms.Setup(x => x.FindByIdAsync(setup.Room.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);
        var result = await setup.Service.CreateAsync(setup.Request);
        AssertFailure(result, archived ? ReservationOperationStatus.RoomInactive : ReservationOperationStatus.RoomNotFound,
            archived ? "reservation.roomInactive" : "reservation.roomNotFound");
        setup.Reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 60, false, true, ReservationOperationStatus.Conflict)]
    [InlineData(-30, 90, false, true, ReservationOperationStatus.Conflict)]
    [InlineData(30, 90, false, true, ReservationOperationStatus.Conflict)]
    [InlineData(-60, 0, false, true, ReservationOperationStatus.Success)]
    [InlineData(60, 120, false, true, ReservationOperationStatus.Success)]
    [InlineData(0, 60, true, true, ReservationOperationStatus.Success)]
    [InlineData(0, 60, false, false, ReservationOperationStatus.Success)]
    public async Task Create_UsesRepositoryAvailabilityWithoutReimplementingDomainGeometry(int from, int until,
        bool cancelled, bool sameRoom, ReservationOperationStatus expected)
    {
        var setup = new Setup();
        var existing = Reservation.Create(sameRoom ? setup.Room.Id : Guid.NewGuid(), Guid.NewGuid(), "Inna",
            new ReservationPeriod(setup.Request.StartAt.AddMinutes(from), setup.Request.StartAt.AddMinutes(until)), setup.Clock);
        if (cancelled) existing.Cancel(setup.Clock);
        setup.Reservations.Setup(x => x.HasActiveConflictAsync(setup.Room.Id, It.IsAny<ReservationPeriod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid roomId, ReservationPeriod period, CancellationToken _) => existing.RoomId == roomId && existing.Blocks(period));
        var result = await setup.Service.CreateAsync(setup.Request);
        Assert.Equal(expected, result.Status);
        setup.Reservations.Verify(x => x.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            expected == ReservationOperationStatus.Success ? Times.Once() : Times.Never());
        if (expected == ReservationOperationStatus.Conflict) AssertFailure(result, expected, "reservation.conflict");
    }

    [Fact]
    public async Task Create_ConflictDuringSaveIsStillConflictAfterFreeAvailabilityCheck()
    {
        var setup = new Setup();
        setup.Reservations.Setup(x => x.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>())).ReturnsAsync(ReservationWriteOutcome.Conflict);
        AssertFailure(await setup.Service.CreateAsync(setup.Request), ReservationOperationStatus.Conflict, "reservation.conflict");
    }

    [Theory]
    [InlineData("room")]
    [InlineData("availability")]
    [InlineData("save")]
    public async Task Create_StorageFailureAtAnyBoundaryHasDistinctSanitizedResult(string boundary)
    {
        var setup = new Setup();
        if (boundary == "room") setup.Rooms.Setup(x => x.FindByIdAsync(setup.Room.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        if (boundary == "availability") setup.Reservations.Setup(x => x.HasActiveConflictAsync(setup.Room.Id, It.IsAny<ReservationPeriod>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        if (boundary == "save") setup.Reservations.Setup(x => x.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        AssertFailure(await setup.Service.CreateAsync(setup.Request), ReservationOperationStatus.InfrastructureFailure, "reservation.storageFailure");
        if (boundary != "save") setup.Reservations.Verify(x => x.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_CancellationPropagatesAndDoesNotTouchRepositories()
    {
        var setup = new Setup();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => setup.Service.CreateAsync(setup.Request, cancellation.Token));
        setup.Rooms.VerifyNoOtherCalls();
        setup.Reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public void Request_HasNoClientControlledIdentityField()
        => Assert.Equal(new[] { "EndAt", "RoomId", "StartAt", "Title" }, typeof(CreateReservationRequest).GetProperties().Select(x => x.Name).OrderBy(x => x));

    private static void AssertFailure(ReservationOperationResult result, ReservationOperationStatus status, string code)
    {
        Assert.Equal(status, result.Status);
        Assert.Equal(code, result.ErrorCode);
        Assert.Null(result.Reservation);
    }

    private sealed class Setup
    {
        internal readonly Mock<IRoomRepository> Rooms = new(MockBehavior.Strict);
        internal readonly Mock<IReservationRepository> Reservations = new(MockBehavior.Strict);
        internal readonly Mock<ICurrentUser> User = new(MockBehavior.Strict);
        internal readonly Guid UserId = Guid.NewGuid();
        internal readonly TimeProvider Clock = new FixedClock();
        internal Room Room { get; }
        internal CreateReservationRequest Request { get; }
        internal CreateReservationService Service { get; }
        internal Setup()
        {
            Room = Room.Create("Sala", null, "Piętro 1", 12, Clock);
            Request = new(Room.Id, "Spotkanie", new(2030, 1, 15, 9, 0, 0, TimeSpan.Zero), new(2030, 1, 15, 10, 0, 0, TimeSpan.Zero));
            User.SetupGet(x => x.UserId).Returns(UserId);
            User.SetupGet(x => x.IsActive).Returns(true);
            Rooms.Setup(x => x.FindByIdAsync(Room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Room);
            Reservations.Setup(x => x.HasActiveConflictAsync(Room.Id, It.IsAny<ReservationPeriod>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            Reservations.Setup(x => x.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>())).ReturnsAsync(ReservationWriteOutcome.Saved);
            Service = new(Rooms.Object, Reservations.Object, User.Object, Clock);
        }
    }
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
