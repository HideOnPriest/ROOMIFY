using Moq;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Application.Rooms;
using Roomify.Domain.Rooms;

namespace Roomify.Application.UnitTests;

public sealed class RoomManagementTests
{
    private readonly Mock<IRoomRepository> rooms = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> user = new(MockBehavior.Strict);
    private readonly ManagementClock clock = new();
    private readonly Room room;
    private readonly CreateRoomRequest create = new("  Wisła  ", "  Opis  ", "  Piętro 2  ", 24);

    public RoomManagementTests()
    {
        room = Room.Create("Poprzednia", null, "Piętro 1", 12, clock);
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        user.SetupGet(x => x.IsActive).Returns(true);
        user.SetupGet(x => x.IsAdministrator).Returns(true);
        rooms.Setup(x => x.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        rooms.Setup(x => x.FindForUpdateAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        rooms.Setup(x => x.NameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        rooms.Setup(x => x.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>())).ReturnsAsync(RoomWriteOutcome.Saved);
        rooms.Setup(x => x.SaveAsync(room, It.IsAny<CancellationToken>())).ReturnsAsync(RoomWriteOutcome.Saved);
    }

    private Task<RoomOperationResult<RoomDetails>> Write(string operation) => operation switch
    {
        "create" => new CreateRoomService(rooms.Object, user.Object, clock).CreateAsync(create),
        "update" => new UpdateRoomService(rooms.Object, user.Object).UpdateAsync(new(room.Id, create.Name, create.Description, create.Location, create.Capacity)),
        "archive" => new ArchiveRoomService(rooms.Object, user.Object).ArchiveAsync(room.Id),
        "activate" => new ActivateRoomService(rooms.Object, user.Object).ActivateAsync(room.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    [Theory]
    [InlineData("create", false, false)] [InlineData("create", false, true)] [InlineData("create", true, false)]
    [InlineData("update", false, false)] [InlineData("update", false, true)] [InlineData("update", true, false)]
    [InlineData("archive", false, false)] [InlineData("archive", false, true)] [InlineData("archive", true, false)]
    [InlineData("activate", false, false)] [InlineData("activate", false, true)] [InlineData("activate", true, false)]
    public async Task Writes_RequireAuthenticatedAdministratorBeforeAnyRead(string operation, bool authenticated, bool administrator)
    {
        user.SetupGet(x => x.UserId).Returns(authenticated ? Guid.NewGuid() : null);
        user.SetupGet(x => x.IsAdministrator).Returns(administrator);
        Fail(await Write(operation), authenticated ? RoomOperationStatus.Forbidden : RoomOperationStatus.Unauthenticated,
            authenticated ? "room.forbidden" : "authentication.required");
        Assert.Empty(rooms.Invocations);
        Assert.True(room.IsActive);
        Assert.Equal("Poprzednia", room.Name);
    }

    [Theory]
    [InlineData("create")] [InlineData("update")] [InlineData("archive")] [InlineData("activate")]
    public async Task EmptyIdentity_DoesNotBecomeAdministrator(string operation)
    {
        user.SetupGet(x => x.UserId).Returns(Guid.Empty);
        Fail(await Write(operation), RoomOperationStatus.Unauthenticated, "authentication.required");
        Assert.Empty(rooms.Invocations);
    }

    [Fact]
    public async Task Create_NormalizesValidDomainDataAndReturnsActiveSnapshot()
    {
        var result = await Write("create");
        Assert.Equal(RoomOperationStatus.Success, result.Status);
        Assert.Null(result.ErrorCode);
        var value = Assert.IsType<RoomDetails>(result.Value);
        Assert.NotEqual(Guid.Empty, value.Id);
        Assert.Equal("Wisła", value.Name); Assert.Equal("Opis", value.Description);
        Assert.Equal("Piętro 2", value.Location); Assert.Equal(24, value.Capacity);
        Assert.True(value.IsActive); Assert.Equal(clock.Now, value.CreatedAt);
        rooms.Verify(x => x.NameExistsAsync("Wisła", null, It.IsAny<CancellationToken>()), Times.Once);
        rooms.Verify(x => x.AddAsync(It.Is<Room>(r => r.Id == value.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    public static IEnumerable<object?[]> InvalidDetails()
    {
        yield return [null, "Piętro", 1, "room.name"];
        yield return [" ", "Piętro", 1, "room.name"];
        yield return [new string('x', 101), "Piętro", 1, "room.name"];
        yield return ["Sala", null, 1, "room.location"];
        yield return ["Sala", " ", 1, "room.location"];
        yield return ["Sala", new string('x', 151), 1, "room.location"];
        yield return ["Sala", "Piętro", 0, "room.capacity"];
        yield return ["Sala", "Piętro", -1, "room.capacity"];
    }

    [Theory] [MemberData(nameof(InvalidDetails))]
    public async Task Create_InvalidDomainDataNeverQueriesOrWrites(string? name, string? location, int capacity, string code)
    {
        Fail(await new CreateRoomService(rooms.Object, user.Object, clock).CreateAsync(new(name, null, location, capacity)), RoomOperationStatus.InvalidInput, code);
        Assert.Empty(rooms.Invocations);
    }

    [Theory] [MemberData(nameof(InvalidDetails))]
    public async Task Update_InvalidDomainDataDoesNotMutateOrSave(string? name, string? location, int capacity, string code)
    {
        var before = RoomDetails.From(room);
        Fail(await new UpdateRoomService(rooms.Object, user.Object).UpdateAsync(new(room.Id, name, null, location, capacity)), RoomOperationStatus.InvalidInput, code);
        Assert.Equal(before, RoomDetails.From(room));
        rooms.Verify(x => x.SaveAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
        rooms.Verify(x => x.NameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DomainBoundaries_AreAcceptedAndArchivedRoomStaysArchivedAfterEdit()
    {
        room.Archive();
        var originalId = room.Id; var created = room.CreatedAt;
        var result = await new UpdateRoomService(rooms.Object, user.Object).UpdateAsync(new(room.Id, new string('x', 100), " ", new string('y', 150), 1));
        Assert.Equal(RoomOperationStatus.Success, result.Status);
        Assert.Equal(originalId, result.Value!.Id); Assert.Equal(created, result.Value.CreatedAt);
        Assert.Equal(1, result.Value.Capacity); Assert.Null(result.Value.Description);
        Assert.False(result.Value.IsActive); Assert.Equal(100, result.Value.Name.Length); Assert.Equal(150, result.Value.Location.Length);
    }

    [Fact]
    public async Task Update_ChangesAllEditableDataPreservingIdentityAndCreatedAt()
    {
        var before = RoomDetails.From(room);
        var result = await Write("update");
        Assert.Equal(RoomOperationStatus.Success, result.Status);
        Assert.Equal(before.Id, result.Value!.Id); Assert.Equal(before.CreatedAt, result.Value.CreatedAt);
        Assert.Equal("Wisła", result.Value.Name); Assert.Equal("Opis", result.Value.Description);
        Assert.Equal("Piętro 2", result.Value.Location); Assert.Equal(24, result.Value.Capacity);
        rooms.Verify(x => x.NameExistsAsync("Wisła", room.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory] [InlineData("create")] [InlineData("update")]
    public async Task PrecheckDuplicate_DoesNotSaveOrChangeTrackedRoom(string operation)
    {
        rooms.Setup(x => x.NameExistsAsync("Wisła", It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var before = RoomDetails.From(room);
        Fail(await Write(operation), RoomOperationStatus.DuplicateName, "room.duplicateName");
        Assert.Equal(before, RoomDetails.From(room));
        rooms.Verify(x => x.SaveAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
        rooms.Verify(x => x.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(RoomWriteOutcome.DuplicateName, RoomOperationStatus.DuplicateName, "room.duplicateName")]
    [InlineData(RoomWriteOutcome.ConcurrentModification, RoomOperationStatus.ConcurrentModification, "room.concurrentModification")]
    [InlineData((RoomWriteOutcome)999, RoomOperationStatus.InfrastructureFailure, "room.storageFailure")]
    public async Task Writes_MapStorageOutcomesWithoutSuccessPayload(RoomWriteOutcome outcome, RoomOperationStatus status, string code)
    {
        rooms.Setup(x => x.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>())).ReturnsAsync(outcome);
        rooms.Setup(x => x.SaveAsync(room, It.IsAny<CancellationToken>())).ReturnsAsync(outcome);
        Fail(await Write("create"), status, code);
        Fail(await Write("update"), status, code);
        Fail(await Write("archive"), status, code);
        // Mock nie wykonuje rollbacku; ponownie ustawiamy stan wyjściowy dla Activate.
        Fail(await Write("activate"), status, code);
    }

    [Theory] [InlineData("update")] [InlineData("archive")] [InlineData("activate")]
    public async Task MissingRoom_IsExplicitAndNeverSaved(string operation)
    {
        rooms.Setup(x => x.FindForUpdateAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);
        Fail(await Write(operation), RoomOperationStatus.NotFound, "room.notFound");
        rooms.Verify(x => x.SaveAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveAndActivate_PreserveDetailsAndReturnExplicitRepeatedStatusesWithoutExtraWrite()
    {
        var before = RoomDetails.From(room);
        var archived = await Write("archive");
        Assert.Equal(RoomOperationStatus.Success, archived.Status); Assert.False(archived.Value!.IsActive);
        Assert.Equal(before with { IsActive = false }, archived.Value);
        Fail(await Write("archive"), RoomOperationStatus.AlreadyArchived, "room.alreadyArchived");
        var activated = await Write("activate");
        Assert.Equal(RoomOperationStatus.Success, activated.Status); Assert.Equal(before, activated.Value);
        Fail(await Write("activate"), RoomOperationStatus.AlreadyActive, "room.alreadyActive");
        rooms.Verify(x => x.SaveAsync(room, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory] [InlineData("create")] [InlineData("update")] [InlineData("archive")] [InlineData("activate")]
    public async Task StorageFailure_IsNeutralAndHasNoPayload(string operation)
    {
        room.Archive();
        rooms.Setup(x => x.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        rooms.Setup(x => x.FindForUpdateAsync(room.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        Fail(await Write(operation), RoomOperationStatus.InfrastructureFailure, "room.storageFailure");
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task GetById_VisibilityIsDeterminedByTrustedRoleAndActivity(bool active, bool administrator)
    {
        if (!active) room.Archive();
        user.SetupGet(x => x.IsAdministrator).Returns(administrator);
        var result = await new GetRoomByIdService(rooms.Object, user.Object).GetAsync(room.Id);
        if (active || administrator) { Assert.Equal(RoomOperationStatus.Success, result.Status); Assert.Equal(RoomDetails.From(room), result.Value); }
        else Fail(result, RoomOperationStatus.NotFound, "room.notFound");
    }

    [Fact]
    public async Task GetById_MissingAndStorageFailureAreDistinct()
    {
        rooms.Setup(x => x.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);
        Fail(await new GetRoomByIdService(rooms.Object, user.Object).GetAsync(room.Id), RoomOperationStatus.NotFound, "room.notFound");
        rooms.Setup(x => x.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        Fail(await new GetRoomByIdService(rooms.Object, user.Object).GetAsync(room.Id), RoomOperationStatus.InfrastructureFailure, "room.storageFailure");
    }

    [Theory] [InlineData(null)] [InlineData("")]
    public async Task Reads_RequireRealIdentityEvenWithAdminFlag(string? identity)
    {
        user.SetupGet(x => x.UserId).Returns(identity is null ? null : Guid.Empty);
        Fail(await new GetRoomByIdService(rooms.Object, user.Object).GetAsync(room.Id), RoomOperationStatus.Unauthenticated, "authentication.required");
        Fail(await new GetRoomsService(rooms.Object, user.Object).GetAsync(new()), RoomOperationStatus.Unauthenticated, "authentication.required");
        Assert.Empty(rooms.Invocations);
    }

    [Theory] [InlineData(false, null)] [InlineData(false, true)] [InlineData(true, null)] [InlineData(true, true)] [InlineData(true, false)]
    public async Task List_ForwardsNormalizedFiltersAndAuthorizedScope(bool administrator, bool? active)
    {
        user.SetupGet(x => x.IsAdministrator).Returns(administrator);
        var expected = new RoomSearch(10, 5, "Wisła", "Piętro", 24, administrator ? active : true, RoomSortBy.Capacity, RoomSortDirection.Descending);
        var page = new RoomPage([RoomDetails.From(room)], 11, 3, 5);
        rooms.Setup(x => x.SearchAsync(expected, It.IsAny<CancellationToken>())).ReturnsAsync(page);
        var result = await new GetRoomsService(rooms.Object, user.Object).GetAsync(new(3, 5, "  Wisła  ", " Piętro ", 24, active, RoomSortBy.Capacity, RoomSortDirection.Descending));
        Assert.Equal(RoomOperationStatus.Success, result.Status); Assert.Equal(page, result.Value);
    }

    [Fact]
    public async Task List_UserCannotRequestInactiveRooms()
    {
        user.SetupGet(x => x.IsAdministrator).Returns(false);
        Fail(await new GetRoomsService(rooms.Object, user.Object).GetAsync(new(IsActive: false)), RoomOperationStatus.Forbidden, "room.forbidden");
        Assert.Empty(rooms.Invocations);
    }

    [Theory]
    [InlineData(0, 20, null, 0, 0)] [InlineData(-1, 20, null, 0, 0)]
    [InlineData(1, 0, null, 0, 0)] [InlineData(1, -1, null, 0, 0)] [InlineData(1, 101, null, 0, 0)]
    [InlineData(int.MaxValue, 100, null, 0, 0)] [InlineData(1, 20, 0, 0, 0)] [InlineData(1, 20, -1, 0, 0)]
    [InlineData(1, 20, null, 99, 0)] [InlineData(1, 20, null, 0, 99)]
    public async Task List_InvalidParametersNeverQuery(int page, int size, int? capacity, int sort, int direction)
    {
        Fail(await new GetRoomsService(rooms.Object, user.Object).GetAsync(new(page, size, MinCapacity: capacity,
            SortBy: (RoomSortBy)sort, SortDirection: (RoomSortDirection)direction)), RoomOperationStatus.InvalidInput, "room.query");
        Assert.Empty(rooms.Invocations);
    }

    [Theory] [InlineData(1, 1)] [InlineData(1, 100)] [InlineData(int.MaxValue, 1)]
    public async Task List_ValidPagingBoundariesAndEmptyResults(int page, int size)
    {
        var empty = new RoomPage([], 0, page, size);
        rooms.Setup(x => x.SearchAsync(It.Is<RoomSearch>(q => q.Offset == page - 1 && q.Limit == size), It.IsAny<CancellationToken>())).ReturnsAsync(empty);
        var result = await new GetRoomsService(rooms.Object, user.Object).GetAsync(new(page, size, MinCapacity: 1));
        Assert.Equal(RoomOperationStatus.Success, result.Status); Assert.Empty(result.Value!.Items); Assert.Equal(0, result.Value.TotalCount);
    }

    [Fact]
    public async Task List_StorageFailureHasNoPage()
    {
        rooms.Setup(x => x.SearchAsync(It.IsAny<RoomSearch>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ReservationStorageException());
        Fail(await new GetRoomsService(rooms.Object, user.Object).GetAsync(new()), RoomOperationStatus.InfrastructureFailure, "room.storageFailure");
    }

    [Fact]
    public async Task NullRequestsAndEmptyIds_AreRejectedWithoutDatabaseAccess()
    {
        Fail(await new CreateRoomService(rooms.Object, user.Object, clock).CreateAsync(null), RoomOperationStatus.InvalidInput, "room.request");
        Fail(await new UpdateRoomService(rooms.Object, user.Object).UpdateAsync(null), RoomOperationStatus.InvalidInput, "room.request");
        Fail(await new UpdateRoomService(rooms.Object, user.Object).UpdateAsync(new(Guid.Empty, "Sala", null, "Piętro", 1)), RoomOperationStatus.InvalidInput, "room.request");
        Fail(await new ArchiveRoomService(rooms.Object, user.Object).ArchiveAsync(Guid.Empty), RoomOperationStatus.InvalidInput, "room.id");
        Fail(await new ActivateRoomService(rooms.Object, user.Object).ActivateAsync(Guid.Empty), RoomOperationStatus.InvalidInput, "room.id");
        Fail(await new GetRoomByIdService(rooms.Object, user.Object).GetAsync(Guid.Empty), RoomOperationStatus.InvalidInput, "room.id");
        Fail(await new GetRoomsService(rooms.Object, user.Object).GetAsync(null), RoomOperationStatus.InvalidInput, "room.query");
        Assert.Empty(rooms.Invocations);
    }

    [Fact]
    public async Task Cancellation_IsPropagatedWithoutDatabaseAccess()
    {
        using var source = new CancellationTokenSource(); source.Cancel(); var token = source.Token;
        await Assert.ThrowsAsync<OperationCanceledException>(() => new CreateRoomService(rooms.Object, user.Object, clock).CreateAsync(create, token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new UpdateRoomService(rooms.Object, user.Object).UpdateAsync(null, token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new ArchiveRoomService(rooms.Object, user.Object).ArchiveAsync(room.Id, token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new ActivateRoomService(rooms.Object, user.Object).ActivateAsync(room.Id, token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new GetRoomByIdService(rooms.Object, user.Object).GetAsync(room.Id, token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new GetRoomsService(rooms.Object, user.Object).GetAsync(new(), token));
        Assert.Empty(rooms.Invocations);
    }

    private static void Fail<T>(RoomOperationResult<T> result, RoomOperationStatus status, string code) where T : class
    { Assert.Equal(status, result.Status); Assert.Equal(code, result.ErrorCode); Assert.Null(result.Value); }
}
