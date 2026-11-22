using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Application.Rooms;
using Roomify.Application.Users;

namespace Roomify.Application.UnitTests;

public sealed class UserManagementTests
{
    private readonly Mock<IUserAccountStore> store = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUser> user = new(MockBehavior.Strict);
    private readonly Guid id = Guid.NewGuid();
    private readonly UserDetails details;
    private readonly string password = "Test-" + Guid.NewGuid().ToString("N") + "aA1";

    public UserManagementTests()
    {
        details = new(id, "user@example.invalid", "Nazwa", true, DateTimeOffset.UnixEpoch);
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        user.SetupGet(x => x.IsActive).Returns(true);
        user.SetupGet(x => x.IsAdministrator).Returns(true);
    }

    private async Task<(UserOperationStatus Status, object? Value, string? Code)> Call(string operation)
    {
        if (operation == "list")
        {
            var page = await new GetUsersService(store.Object, user.Object).GetAsync(new()); return (page.Status, page.Value, page.ErrorCode);
        }
        var result = operation switch
        {
            "create" => await new CreateUserService(store.Object, user.Object).CreateAsync(new("user@example.invalid", "Nazwa", password)),
            "update" => await new UpdateUserService(store.Object, user.Object).UpdateAsync(new(id, "user@example.invalid", "Nazwa")),
            "get" => await new GetUserByIdService(store.Object, user.Object).GetAsync(id),
            "deactivate" => await new DeactivateUserService(store.Object, user.Object).DeactivateAsync(id),
            "activate" => await new ActivateUserService(store.Object, user.Object).ActivateAsync(id),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        return (result.Status, result.Value, result.ErrorCode);
    }

    public static IEnumerable<object[]> DeniedCases()
    {
        foreach (var operation in new[] { "create", "update", "get", "list", "deactivate", "activate" })
        foreach (var state in new[] { "anonymous", "empty", "user", "inactiveAdmin" }) yield return [operation, state];
    }
    [Theory] [MemberData(nameof(DeniedCases))]
    public async Task AllOperations_RequireActiveAuthenticatedAdministratorBeforeStore(string operation, string state)
    {
        if (state == "anonymous") user.SetupGet(x => x.UserId).Returns((Guid?)null);
        if (state == "empty") user.SetupGet(x => x.UserId).Returns(Guid.Empty);
        if (state == "user") user.SetupGet(x => x.IsAdministrator).Returns(false);
        if (state == "inactiveAdmin") user.SetupGet(x => x.IsActive).Returns(false);
        var result = await Call(operation);
        Assert.Equal(state is "anonymous" or "empty" ? UserOperationStatus.Unauthenticated : UserOperationStatus.Forbidden, result.Status);
        Assert.Null(result.Value); Assert.Equal(state is "anonymous" or "empty" ? "authentication.required" : "user.forbidden", result.Code); Assert.Empty(store.Invocations);
    }

    [Fact]
    public async Task Create_NormalizesProfileButDoesNotAlterPasswordOrAllowRoleInput()
    {
        store.Setup(x => x.CreateAsync(It.Is<CreateUserRequest>(q => q.Email == details.Email && q.DisplayName == "Nazwa" && q.InitialPassword == password), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserOperationResult<UserDetails>.Success(details));
        var request = new CreateUserRequest(" user@example.invalid ", " Nazwa ", password);
        var result = await new CreateUserService(store.Object, user.Object).CreateAsync(request);
        Assert.Equal(UserOperationStatus.Success, result.Status); Assert.Equal(details, result.Value);
        Assert.DoesNotContain(password, JsonSerializer.Serialize(result)); Assert.DoesNotContain(password, request.ToString());
        Assert.Equal(new[] { "DisplayName", "Email", "InitialPassword" }, typeof(CreateUserRequest).GetProperties().Select(x => x.Name).Order());
        Assert.Equal(new[] { "DisplayName", "Email", "UserId" }, typeof(UpdateUserRequest).GetProperties().Select(x => x.Name).Order());
    }

    [Theory] [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("invalid")]
    [InlineData("X <user@example.invalid>")] [InlineData("user@example.invalid\nAttack")]
    public async Task InvalidEmail_NeverCallsIdentityAdapter(string? email)
    {
        var create = await new CreateUserService(store.Object, user.Object).CreateAsync(new(email, null, password));
        Assert.Equal(UserOperationStatus.InvalidInput, create.Status); Assert.Equal("user.input",create.ErrorCode); Assert.Null(create.Value);
        var update = await new UpdateUserService(store.Object, user.Object).UpdateAsync(new(id, email, null));
        Assert.Equal(UserOperationStatus.InvalidInput, update.Status); Assert.Equal("user.input",update.ErrorCode); Assert.Null(update.Value);
        Assert.Empty(store.Invocations);
    }

    [Theory] [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public async Task MissingPassword_NeverCallsIdentity(string? value)
    {
        var result = await new CreateUserService(store.Object, user.Object).CreateAsync(new(details.Email, null, value));
        Assert.Equal(UserOperationStatus.InvalidInput, result.Status); Assert.Equal("user.input",result.ErrorCode); Assert.Null(result.Value); Assert.Empty(store.Invocations);
    }

    [Fact]
    public async Task EmailLength_UsesExisting256CharacterModelBoundary()
    {
        var email=new string('a',64)+"@"+new string('b',63)+"."+new string('c',63)+"."+new string('d',63);
        Assert.Equal(256,email.Length);
        store.Setup(x=>x.CreateAsync(It.Is<CreateUserRequest>(q=>q.Email==email),It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserOperationResult<UserDetails>.Success(details with{Email=email}));
        Assert.Equal(UserOperationStatus.Success,(await new CreateUserService(store.Object,user.Object).CreateAsync(new(email,null,password))).Status);
        var invalid=await new CreateUserService(store.Object,user.Object).CreateAsync(new(email+"d",null,password));
        Assert.Equal(UserOperationStatus.InvalidInput,invalid.Status);Assert.Equal("user.input",invalid.ErrorCode);Assert.Null(invalid.Value);
        store.Verify(x=>x.CreateAsync(It.IsAny<CreateUserRequest>(),It.IsAny<CancellationToken>()),Times.Once);
    }

    [Theory]
    [InlineData(UserOperationStatus.InvalidInput, "user.identityValidation")]
    [InlineData(UserOperationStatus.DuplicateEmail, "user.duplicateEmail")]
    [InlineData(UserOperationStatus.InfrastructureFailure, "user.roleAssignmentFailed")]
    [InlineData(UserOperationStatus.ConcurrentModification, "user.concurrentModification")]
    public async Task Create_PropagatesControlledIdentityFailureWithoutSensitivePayload(UserOperationStatus status, string code)
    {
        store.Setup(x => x.CreateAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserDetails>.Failure(status, code));
        var result = await Call("create"); Assert.Equal(status, result.Status); Assert.Equal(code, result.Code); Assert.Null(result.Value);
    }

    [Fact]
    public async Task Update_NormalizesAndPreservesReturnedIdentityAndTime()
    {
        store.Setup(x => x.UpdateAsync(new(id, details.Email, "Nazwa"), It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserDetails>.Success(details));
        var result = await new UpdateUserService(store.Object, user.Object).UpdateAsync(new(id, " user@example.invalid ", " Nazwa "));
        Assert.Equal(UserOperationStatus.Success, result.Status); Assert.Equal(details, result.Value);
    }

    [Theory] [InlineData(UserOperationStatus.DuplicateEmail)] [InlineData(UserOperationStatus.ConcurrentModification)] [InlineData(UserOperationStatus.UserNotFound)]
    public async Task Update_DistinguishesStorageFailures(UserOperationStatus status)
    {
        store.Setup(x => x.UpdateAsync(It.IsAny<UpdateUserRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserDetails>.Failure(status, "safe.code"));
        var result = await Call("update"); Assert.Equal(status, result.Status); Assert.Null(result.Value);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Activity_ChangesOnlyActivityOfRequestedAccountAndReportsRepeatedState(bool active)
    {
        var changed = details with { IsActive = active };
        store.Setup(x => x.SetActivityAsync(id, active, It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserDetails>.Success(changed));
        var result = await Call(active ? "activate" : "deactivate");
        Assert.Equal(UserOperationStatus.Success, result.Status); Assert.Equal(changed, result.Value);
        var status = active ? UserOperationStatus.AlreadyActive : UserOperationStatus.AlreadyInactive;
        store.Setup(x => x.SetActivityAsync(id, active, It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserDetails>.Failure(status, "user.state"));
        Assert.Equal(status, (await Call(active ? "activate" : "deactivate")).Status);
    }

    [Theory] [InlineData(UserOperationStatus.LastAdministrator)] [InlineData(UserOperationStatus.UserNotFound)] [InlineData(UserOperationStatus.ConcurrentModification)]
    public async Task Deactivate_PropagatesProtectedStateOrMissingAccount(UserOperationStatus status)
    {
        store.Setup(x => x.SetActivityAsync(id, false, It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserDetails>.Failure(status, "user.state"));
        var result = await Call("deactivate"); Assert.Equal(status, result.Status); Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetById_ReturnsOnlyApprovedProfileFieldsAndExplicitNotFound()
    {
        store.Setup(x => x.FindAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserDetails>.Success(details));
        Assert.Equal(details, (await Call("get")).Value);
        Assert.Equal(new[] { "CreatedAt", "DisplayName", "Email", "Id", "IsActive" }, typeof(UserDetails).GetProperties().Select(x => x.Name).Order());
        store.Setup(x => x.FindAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserDetails>.Failure(UserOperationStatus.UserNotFound, "user.notFound"));
        Assert.Equal(UserOperationStatus.UserNotFound, (await Call("get")).Status);
    }

    [Fact]
    public async Task List_ForwardsNormalizedFiltersAndPagingAndReturnsSafeEmptyPage()
    {
        var query = new UserSearch(6, 3, "Kowalski", false, UserSortBy.DisplayName, UserSortDirection.Descending);
        var page = new UserPage([], 0, 3, 3);
        store.Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(UserOperationResult<UserPage>.Success(page));
        var result = await new GetUsersService(store.Object, user.Object).GetAsync(new(3, 3, " Kowalski ", false, UserSortBy.DisplayName, UserSortDirection.Descending));
        Assert.Equal(UserOperationStatus.Success, result.Status); Assert.Equal(page, result.Value); Assert.Empty(result.Value!.Items);
    }

    [Theory]
    [InlineData(0,20,0,0)] [InlineData(-1,20,0,0)] [InlineData(1,0,0,0)] [InlineData(1,-1,0,0)] [InlineData(1,101,0,0)]
    [InlineData(int.MaxValue,100,0,0)] [InlineData(1,20,99,0)] [InlineData(1,20,0,99)]
    public async Task List_InvalidQueryDoesNotReachStore(int page, int size, int sort, int direction)
    {
        var result = await new GetUsersService(store.Object, user.Object).GetAsync(new(page,size,SortBy:(UserSortBy)sort,SortDirection:(UserSortDirection)direction));
        Assert.Equal(UserOperationStatus.InvalidInput, result.Status); Assert.Equal("user.query",result.ErrorCode); Assert.Null(result.Value); Assert.Empty(store.Invocations);
    }

    [Theory] [InlineData(1,1)] [InlineData(1,100)] [InlineData(int.MaxValue,1)]
    public async Task List_AcceptsPagingBoundaries(int page, int size)
    {
        store.Setup(x => x.SearchAsync(It.Is<UserSearch>(q => q.Offset == page-1 && q.Limit == size),It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserOperationResult<UserPage>.Success(new([],0,page,size)));
        Assert.Equal(UserOperationStatus.Success,(await new GetUsersService(store.Object,user.Object).GetAsync(new(page,size))).Status);
    }

    [Fact]
    public async Task NullRequestsAndEmptyIds_DoNotReachAdapter()
    {
        Assert.Equal(UserOperationStatus.InvalidInput,(await new CreateUserService(store.Object,user.Object).CreateAsync(null)).Status);
        Assert.Equal(UserOperationStatus.InvalidInput,(await new UpdateUserService(store.Object,user.Object).UpdateAsync(null)).Status);
        Assert.Equal(UserOperationStatus.InvalidInput,(await new UpdateUserService(store.Object,user.Object).UpdateAsync(new(Guid.Empty,details.Email,null))).Status);
        Assert.Equal(UserOperationStatus.InvalidInput,(await new GetUsersService(store.Object,user.Object).GetAsync(null)).Status);
        var missingId=await new GetUserByIdService(store.Object,user.Object).GetAsync(Guid.Empty);
        Assert.Equal(UserOperationStatus.InvalidInput,missingId.Status);Assert.Equal("user.id",missingId.ErrorCode);
        var activity=await new ActivateUserService(store.Object,user.Object).ActivateAsync(Guid.Empty);
        Assert.Equal(UserOperationStatus.InvalidInput,activity.Status);Assert.Equal("user.id",activity.ErrorCode);
        Assert.Equal(UserOperationStatus.InvalidInput,(await new DeactivateUserService(store.Object,user.Object).DeactivateAsync(Guid.Empty)).Status);
        Assert.Empty(store.Invocations);
    }

    [Fact]
    public async Task InactiveAccount_CannotUseAnyEarlierRoomOrReservationService()
    {
        user.SetupGet(x=>x.IsActive).Returns(false);
        var rooms = new Mock<IRoomRepository>(MockBehavior.Strict); var reservations = new Mock<IReservationRepository>(MockBehavior.Strict);
        var clock = TimeProvider.System;
        var create = await new CreateReservationService(rooms.Object,reservations.Object,user.Object,clock).CreateAsync(null);
        Assert.Equal(ReservationOperationStatus.Forbidden,create.Status); Assert.Equal("reservation.forbidden",create.ErrorCode);
        var update = await new UpdateReservationService(reservations.Object,rooms.Object,user.Object,clock).UpdateAsync(null);
        Assert.Equal(ReservationOperationStatus.Forbidden,update.Status); Assert.Equal("reservation.forbidden",update.ErrorCode);
        var cancel = await new CancelReservationService(reservations.Object,user.Object,clock).CancelAsync(null);
        Assert.Equal(ReservationOperationStatus.Forbidden,cancel.Status); Assert.Equal("reservation.forbidden",cancel.ErrorCode);
        Assert.Equal(RoomOperationStatus.Forbidden,(await new CreateRoomService(rooms.Object,user.Object,clock).CreateAsync(null)).Status);
        Assert.Equal(RoomOperationStatus.Forbidden,(await new UpdateRoomService(rooms.Object,user.Object).UpdateAsync(null)).Status);
        Assert.Equal(RoomOperationStatus.Forbidden,(await new ArchiveRoomService(rooms.Object,user.Object).ArchiveAsync(id)).Status);
        Assert.Equal(RoomOperationStatus.Forbidden,(await new ActivateRoomService(rooms.Object,user.Object).ActivateAsync(id)).Status);
        Assert.Equal(RoomOperationStatus.Forbidden,(await new GetRoomByIdService(rooms.Object,user.Object).GetAsync(id)).Status);
        Assert.Equal(RoomOperationStatus.Forbidden,(await new GetRoomsService(rooms.Object,user.Object).GetAsync(new())).Status);
        Assert.Empty(rooms.Invocations); Assert.Empty(reservations.Invocations);
    }

    [Fact]
    public async Task CancelledToken_IsPropagatedByAllSixServices()
    {
        using var source=new CancellationTokenSource();source.Cancel();var ct=source.Token;
        await Assert.ThrowsAsync<OperationCanceledException>(()=>new CreateUserService(store.Object,user.Object).CreateAsync(null,ct));
        await Assert.ThrowsAsync<OperationCanceledException>(()=>new UpdateUserService(store.Object,user.Object).UpdateAsync(null,ct));
        await Assert.ThrowsAsync<OperationCanceledException>(()=>new GetUserByIdService(store.Object,user.Object).GetAsync(id,ct));
        await Assert.ThrowsAsync<OperationCanceledException>(()=>new GetUsersService(store.Object,user.Object).GetAsync(null,ct));
        await Assert.ThrowsAsync<OperationCanceledException>(()=>new ActivateUserService(store.Object,user.Object).ActivateAsync(id,ct));
        await Assert.ThrowsAsync<OperationCanceledException>(()=>new DeactivateUserService(store.Object,user.Object).DeactivateAsync(id,ct));
        Assert.Empty(store.Invocations);
    }
}
