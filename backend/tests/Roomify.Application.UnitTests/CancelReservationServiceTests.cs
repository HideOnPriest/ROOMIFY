using Moq;
using Roomify.Application.Reservations;
using Roomify.Domain.Reservations;
using static Roomify.Application.UnitTests.ReservationManagementTestContext;

namespace Roomify.Application.UnitTests;

public sealed class CancelReservationServiceTests
{
    [Theory]
    [InlineData(true, false, ReservationOperationStatus.Success)]
    [InlineData(false, true, ReservationOperationStatus.Success)]
    [InlineData(true, true, ReservationOperationStatus.Success)]
    [InlineData(false, false, ReservationOperationStatus.Forbidden)]
    public async Task Cancel_OnlyOwnerOrAdministratorCanCancelAndHistoryIsPreserved(bool owner, bool administrator, ReservationOperationStatus expected)
    {
        // Arrange: serwis nie ma metody Delete w kontrakcie repozytorium.
        var test = new ReservationManagementTestContext();
        var before = (test.Reservation.Id, test.Reservation.UserId, test.Reservation.RoomId, test.Reservation.Title, test.Reservation.Period, test.Reservation.CreatedAt);
        test.User.SetupGet(x => x.UserId).Returns(owner ? test.Reservation.UserId : Guid.NewGuid());
        test.User.SetupGet(x => x.IsAdministrator).Returns(administrator);
        test.Clock.Now = test.Clock.Now.AddHours(1);
        using var cancellation = new CancellationTokenSource();
        // Act
        var result = await test.CancelService.CancelAsync(new(test.Reservation.Id), cancellation.Token);
        // Assert
        Assert.Equal(expected, result.Status);
        Assert.Equal(before, (test.Reservation.Id, test.Reservation.UserId, test.Reservation.RoomId, test.Reservation.Title, test.Reservation.Period, test.Reservation.CreatedAt));
        if (expected == ReservationOperationStatus.Success)
        {
            Assert.NotNull(result.Reservation);
            Assert.Null(result.ErrorCode);
            Assert.Equal(ReservationStatus.Cancelled, result.Reservation.Status);
            Assert.Equal(test.Clock.Now, result.Reservation.UpdatedAt);
            Assert.False(test.Reservation.Blocks(test.Reservation.Period));
            test.Reservations.Verify(x => x.SaveAsync(test.Reservation, cancellation.Token), Times.Once);
        }
        else
        {
            AssertFailure(result, expected, "reservation.forbidden");
            Assert.Equal(ReservationStatus.Active, test.Reservation.Status);
            Assert.Null(test.Reservation.UpdatedAt);
            test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        test.Rooms.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_MissingIdentityIsNotForbiddenEvenWithAdministratorFlag(bool empty)
    {
        var test = new ReservationManagementTestContext();
        test.User.SetupGet(x => x.UserId).Returns(empty ? Guid.Empty : null);
        test.User.SetupGet(x => x.IsAdministrator).Returns(true);
        AssertFailure(await test.CancelService.CancelAsync(new(test.Reservation.Id)), ReservationOperationStatus.Unauthenticated, "reservation.unauthenticated");
        test.Reservations.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_NullRequestOrEmptyIdDoesNotReadStorage(bool nullRequest)
    {
        var test = new ReservationManagementTestContext();
        AssertFailure(await test.CancelService.CancelAsync(nullRequest ? null : new(Guid.Empty)), ReservationOperationStatus.InvalidInput, "reservation.idRequired");
        test.Reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_MissingReservationHasDistinctResult()
    {
        var test = new ReservationManagementTestContext();
        test.Reservations.Setup(x => x.FindByIdAsync(test.Reservation.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Reservation?)null);
        AssertFailure(await test.CancelService.CancelAsync(new(test.Reservation.Id)), ReservationOperationStatus.ReservationNotFound, "reservation.notFound");
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_RepeatedCancellationIsExplicitAndDoesNotRevealStateToUnauthorizedUser(bool unauthorized)
    {
        var test = new ReservationManagementTestContext();
        test.Reservation.Cancel(test.Clock);
        var updated = test.Reservation.UpdatedAt;
        test.Clock.Now = test.Reservation.EndAt.AddDays(1);
        if (unauthorized) test.User.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        AssertFailure(await test.CancelService.CancelAsync(new(test.Reservation.Id)),
            unauthorized ? ReservationOperationStatus.Forbidden : ReservationOperationStatus.AlreadyCancelled,
            unauthorized ? "reservation.forbidden" : "reservation.alreadyCancelled");
        Assert.Equal(ReservationStatus.Cancelled, test.Reservation.Status);
        Assert.Equal(updated, test.Reservation.UpdatedAt);
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Cancel_StartedReservationIsRejectedWithoutMutation(int ticks)
    {
        var test = new ReservationManagementTestContext();
        test.Clock.Now = test.Reservation.StartAt.AddTicks(ticks);
        AssertFailure(await test.CancelService.CancelAsync(new(test.Reservation.Id)), ReservationOperationStatus.InvalidInput, "BR-017");
        Assert.Equal(ReservationStatus.Active, test.Reservation.Status);
        Assert.Null(test.Reservation.UpdatedAt);
        test.Reservations.Verify(x => x.SaveAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_ConcurrentModificationIsNotReservationConflict()
    {
        var test = new ReservationManagementTestContext();
        test.Reservations.Setup(x => x.SaveAsync(test.Reservation, It.IsAny<CancellationToken>())).ReturnsAsync(ReservationWriteOutcome.ConcurrentModification);
        AssertFailure(await test.CancelService.CancelAsync(new(test.Reservation.Id)), ReservationOperationStatus.ConcurrentModification, "reservation.concurrentModification");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_StorageFailureHasNeutralDistinctResult(bool duringSave)
    {
        var test = new ReservationManagementTestContext();
        if (duringSave) test.Reservations.Setup(x => x.SaveAsync(test.Reservation, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        else test.Reservations.Setup(x => x.FindByIdAsync(test.Reservation.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        AssertFailure(await test.CancelService.CancelAsync(new(test.Reservation.Id)), ReservationOperationStatus.InfrastructureFailure, "reservation.storageFailure");
    }

    [Fact]
    public async Task Cancel_CancelledTokenNeverReadsStorage()
    {
        var test = new ReservationManagementTestContext();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.CancelService.CancelAsync(new(test.Reservation.Id), new CancellationToken(true)));
        test.Reservations.VerifyNoOtherCalls();
    }

    [Fact]
    public void Requests_CannotSetOwnerRoomRoleOrStatus()
    {
        Assert.Equal(new[] { "EndAt", "ReservationId", "StartAt", "Title" }, typeof(UpdateReservationRequest).GetProperties().Select(x => x.Name).OrderBy(x => x));
        Assert.Equal(new[] { "ReservationId" }, typeof(CancelReservationRequest).GetProperties().Select(x => x.Name));
    }
}
