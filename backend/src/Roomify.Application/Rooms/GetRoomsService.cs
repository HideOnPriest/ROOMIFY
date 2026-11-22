using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;

namespace Roomify.Application.Rooms;

public sealed class GetRoomsService(IRoomRepository rooms, ICurrentUser user)
{
    public async Task<RoomOperationResult<RoomPage>> GetAsync(GetRoomsRequest? request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RoomRules.Authorize(user, false) is { } denied) return RoomRules.Denied<RoomPage>(denied);
        if (request is null || !RoomRules.Valid(request))
            return RoomOperationResult<RoomPage>.Failure(RoomOperationStatus.InvalidInput, "room.query");
        if (!user.IsAdministrator && request.IsActive == false) return RoomRules.Denied<RoomPage>(RoomOperationStatus.Forbidden);
        var query = new RoomSearch((request.PageNumber - 1) * request.PageSize, request.PageSize,
            request.Search?.Trim(), request.Location?.Trim(), request.MinCapacity,
            user.IsAdministrator ? request.IsActive : true, request.SortBy, request.SortDirection);
        try { return RoomOperationResult<RoomPage>.Success(await rooms.SearchAsync(query, cancellationToken)); }
        catch (ReservationStorageException) { return RoomOperationResult<RoomPage>.Failure(RoomOperationStatus.InfrastructureFailure, "room.storageFailure"); }
    }
}
