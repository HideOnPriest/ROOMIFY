using Moq;
using Roomify.Application.Reservations;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;
using static Roomify.Application.UnitTests.ReservationManagementTestContext;

namespace Roomify.Application.UnitTests;

public sealed class UpdateReservationServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_ChangesTitleAndOptionalPeriodWithoutChangingIdentityOrOwner(bool samePeriod)
    {
        // Arrange: ten sam obiekt domenowy musi dotrzeć do zapisu, nie nowa rezerwacja.
        var test = new ReservationManagementTestContext();
        var before = (test.Reservation.Id, test.Reservation.RoomId, test.Reservation.UserId, test.Reservation.CreatedAt);
        var request = samePeriod ? test.Update with { StartAt = test.Reservation.StartAt, EndAt = test.Reservation.EndAt } : test.Update;
        test.Clock.Now = test.Clock.Now.AddHours(1);
        using var cancellation = new CancellationTokenSource();
        var result = await test.UpdateService.UpdateAsync(request, cancellation.Token);
        // Assert: właściciel/sala/Id pozostają identyczne, a tytuł jest normalizowany przez Domain.
        Assert.Equal(ReservationOperationStatus.Success, result.Status);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Reservation);
        Assert.Equal(before, (test.Reservation.Id, test.Reservation.RoomId, test.Reservation.UserId, test.Reservation.CreatedAt));
        Assert.Equal("Nowy tytuł", test.Reservation.Title);
        Assert.Equal(new ReservationPeriod(request.StartAt, request.EndAt), test.Reservation.Period);
        Assert.Equal(test.Clock.Now, result.Reservation.UpdatedAt);
        Assert.Equal(test.Reservation.Title, result.Reservation.Title);
        Assert.Equal(test.Reservation.UserId, result.Reservation.UserId);
        test.Reservations.Verify(x => x.HasActiveConflictExcludingAsync(test.Room.Id,
            new ReservationPeriod(request.StartAt, request.EndAt), test.Reservation.Id, cancellation.Token), Times.Once);
        test.Reservations.Verify(x => x.SaveAsync(test.Reservation, cancellation.Token), Times.Once);
        test.Reservations.Verify(x => x.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, false, ReservationOperationStatus.Success)]
    [InlineData(true, true, ReservationOperationStatus.Success)]
    [InlineData(false, true, ReservationOperationStatus.Success)]
    [InlineData(false, false, ReservationOperationStatus.Forbidden)]
    public async Task Update_OnlyOwnerOrTrustedAdministratorCanManage(bool owner, bool administrator, ReservationOperationStatus expected)
    {
        var test = new ReservationManagementTestContext();
        test.User.SetupGet(x => x.UserId).Returns(owner ? test.Reservation.UserId : Guid.NewGuid());
        test.User.SetupGet(x => x.IsAdministrator).Returns(administrator);
        var result = await test.UpdateService.UpdateAsync(test.Update);
        Assert.Equal(expected, result.Status);
        if (expected == ReservationOperationStatus.Forbidden)
        {
            AssertFailure(result, expected, "reservation.forbidden");
            Assert.Equal("Pierwotny tytuł", test.Reservation.Title);
            Assert.Null(test.Reservation.UpdatedAt);
            test.Rooms.VerifyNoOtherCalls();
            test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_NoIdentityIsDistinctFromForbiddenEvenWhenAdministratorFlagIsTrue(bool empty)
    {
        var test = new ReservationManagementTestContext();
        test.User.SetupGet(x => x.UserId).Returns(empty ? Guid.Empty : null);
        test.User.SetupGet(x => x.IsAdministrator).Returns(true);
        AssertFailure(await test.UpdateService.UpdateAsync(test.Update), ReservationOperationStatus.Unauthenticated, "reservation.unauthenticated");
        test.Reservations.VerifyNoOtherCalls();
        test.Rooms.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_NullRequestOrEmptyIdDoesNotReadStorage(bool nullRequest)
    {
        var test = new ReservationManagementTestContext();
        AssertFailure(await test.UpdateService.UpdateAsync(nullRequest ? null : test.Update with { ReservationId = Guid.Empty }),
            ReservationOperationStatus.InvalidInput, "reservation.idRequired");
        test.Reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_MissingReservationHasNoFurtherReadsOrWrites()
    {
        var test = new ReservationManagementTestContext();
        test.Reservations.Setup(x => x.FindByIdAsync(test.Reservation.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Reservation?)null);
        AssertFailure(await test.UpdateService.UpdateAsync(test.Update), ReservationOperationStatus.ReservationNotFound, "reservation.notFound");
        test.Rooms.VerifyNoOtherCalls();
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_CancelledStateIsOnlyVisibleAfterAuthorization(bool unauthorized)
    {
        var test = new ReservationManagementTestContext();
        test.Reservation.Cancel(test.Clock);
        var updated = test.Reservation.UpdatedAt;
        if (unauthorized) test.User.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        AssertFailure(await test.UpdateService.UpdateAsync(test.Update),
            unauthorized ? ReservationOperationStatus.Forbidden : ReservationOperationStatus.AlreadyCancelled,
            unauthorized ? "reservation.forbidden" : "reservation.alreadyCancelled");
        Assert.Equal(updated, test.Reservation.UpdatedAt);
        test.Rooms.VerifyNoOtherCalls();
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("title-null", "BR-024")]
    [InlineData("title-blank", "BR-024")]
    [InlineData("title-long", "BR-024")]
    [InlineData("equal", "BR-001")]
    [InlineData("reversed", "BR-001")]
    [InlineData("duration", "BR-007")]
    [InlineData("opening", "BR-008")]
    [InlineData("closing", "BR-008")]
    [InlineData("past", "BR-002")]
    [InlineData("minute", "BR-015")]
    [InlineData("day", "BR-014")]
    public async Task Update_InvalidDataIsAtomicAndNeverSaved(string scenario, string code)
    {
        var test = new ReservationManagementTestContext();
        var original = (test.Reservation.Title, test.Reservation.Period, test.Reservation.UpdatedAt);
        var request = scenario switch
        {
            "title-null" => test.Update with { Title = null },
            "title-blank" => test.Update with { Title = " \t" },
            "title-long" => test.Update with { Title = new string('T', 121) },
            "equal" => test.Update with { EndAt = test.Update.StartAt },
            "reversed" => test.Update with { EndAt = test.Update.StartAt.AddMinutes(-1) },
            "duration" => test.Update with { EndAt = test.Update.StartAt.AddHours(8).AddMinutes(1) },
            "opening" => test.Update with { StartAt = test.Reservation.StartAt.AddHours(-3), EndAt = test.Reservation.StartAt.AddHours(-2) },
            "closing" => test.Update with { StartAt = test.Reservation.StartAt.AddHours(9), EndAt = test.Reservation.StartAt.AddHours(10).AddMinutes(1) },
            "past" => test.Update with { StartAt = test.Clock.Now.AddHours(-1), EndAt = test.Clock.Now },
            "minute" => test.Update with { StartAt = test.Update.StartAt.AddTicks(1) },
            "day" => test.Update with { StartAt = test.Reservation.StartAt.AddHours(13).AddMinutes(30), EndAt = test.Reservation.StartAt.AddHours(14).AddMinutes(30) },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        AssertFailure(await test.UpdateService.UpdateAsync(request), ReservationOperationStatus.InvalidInput, code);
        Assert.Equal(original, (test.Reservation.Title, test.Reservation.Period, test.Reservation.UpdatedAt));
        test.Rooms.VerifyNoOtherCalls();
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Update_StartedReservationCannotBeEdited(int ticks)
    {
        var test = new ReservationManagementTestContext();
        test.Clock.Now = test.Reservation.StartAt.AddTicks(ticks);
        AssertFailure(await test.UpdateService.UpdateAsync(test.Update), ReservationOperationStatus.InvalidInput, "BR-017");
        Assert.Null(test.Reservation.UpdatedAt);
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_RoomMustExistAndBeActiveWithoutChangingTrackedReservation(bool archived)
    {
        var test = new ReservationManagementTestContext();
        if (archived) test.Room.Archive();
        else test.Rooms.Setup(x => x.FindByIdAsync(test.Room.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);
        AssertFailure(await test.UpdateService.UpdateAsync(test.Update), archived ? ReservationOperationStatus.RoomInactive : ReservationOperationStatus.RoomNotFound,
            archived ? "reservation.roomInactive" : "reservation.roomNotFound");
        Assert.Equal("Pierwotny tytuł", test.Reservation.Title);
        Assert.Null(test.Reservation.UpdatedAt);
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_PrecheckConflictDoesNotMutateOrSave()
    {
        var test = new ReservationManagementTestContext();
        test.Reservations.Setup(x => x.HasActiveConflictExcludingAsync(test.Room.Id, It.IsAny<ReservationPeriod>(), test.Reservation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        AssertFailure(await test.UpdateService.UpdateAsync(test.Update), ReservationOperationStatus.Conflict, "reservation.conflict");
        Assert.Equal("Pierwotny tytuł", test.Reservation.Title);
        Assert.Null(test.Reservation.UpdatedAt);
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ReservationWriteOutcome.Conflict, ReservationOperationStatus.Conflict, "reservation.conflict")]
    [InlineData(ReservationWriteOutcome.ConcurrentModification, ReservationOperationStatus.ConcurrentModification, "reservation.concurrentModification")]
    [InlineData((ReservationWriteOutcome)999, ReservationOperationStatus.InfrastructureFailure, "reservation.storageFailure")]
    public async Task Update_SaveErrorsHaveDistinctResults(ReservationWriteOutcome outcome, ReservationOperationStatus status, string code)
    {
        var test = new ReservationManagementTestContext();
        test.Reservations.Setup(x => x.SaveAsync(test.Reservation, It.IsAny<CancellationToken>())).ReturnsAsync(outcome);
        AssertFailure(await test.UpdateService.UpdateAsync(test.Update), status, code);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("room")]
    [InlineData("availability")]
    [InlineData("save")]
    public async Task Update_StorageFailureIsNotValidationOrConflict(string boundary)
    {
        var test = new ReservationManagementTestContext();
        if (boundary == "read") test.Reservations.Setup(x => x.FindByIdAsync(test.Reservation.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        if (boundary == "room") test.Rooms.Setup(x => x.FindByIdAsync(test.Room.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        if (boundary == "availability") test.Reservations.Setup(x => x.HasActiveConflictExcludingAsync(test.Room.Id, It.IsAny<ReservationPeriod>(), test.Reservation.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        if (boundary == "save") test.Reservations.Setup(x => x.SaveAsync(test.Reservation, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        AssertFailure(await test.UpdateService.UpdateAsync(test.Update), ReservationOperationStatus.InfrastructureFailure, "reservation.storageFailure");
    }

    [Fact]
    public async Task Update_CancelledTokenDoesNotReadStorage()
    {
        var test = new ReservationManagementTestContext();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.UpdateService.UpdateAsync(test.Update, new CancellationToken(true)));
        test.Reservations.VerifyNoOtherCalls();
    }
}
