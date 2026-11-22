using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Domain.Errors;

namespace Roomify.Application.Rooms;

public sealed class UpdateRoomService(IRoomRepository rooms, ICurrentUser user)
{
    public async Task<RoomOperationResult<RoomDetails>> UpdateAsync(UpdateRoomRequest? request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RoomRules.Authorize(user, true) is { } denied) return RoomRules.Denied<RoomDetails>(denied);
        if (request is null || request.RoomId == Guid.Empty)
            return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InvalidInput, "room.request");
        try
        {
            var room = await rooms.FindForUpdateAsync(request.RoomId, cancellationToken);
            if (room is null) return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.NotFound, "room.notFound");
            room.ValidateUpdate(request.Name, request.Description, request.Location, request.Capacity);
            if (await rooms.NameExistsAsync(request.Name!.Trim(), room.Id, cancellationToken))
                return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.DuplicateName, "room.duplicateName");
            room.UpdateDetails(request.Name, request.Description, request.Location, request.Capacity);
            return RoomRules.Saved(room, await rooms.SaveAsync(room, cancellationToken));
        }
        catch (DomainException error) { return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InvalidInput, error.Code); }
        catch (ReservationStorageException) { return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InfrastructureFailure, "room.storageFailure"); }
    }
}
