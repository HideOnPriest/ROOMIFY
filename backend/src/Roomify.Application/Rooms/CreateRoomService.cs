using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Domain.Errors;
using Roomify.Domain.Rooms;

namespace Roomify.Application.Rooms;

public sealed class CreateRoomService(IRoomRepository rooms, ICurrentUser user, TimeProvider clock)
{
    public async Task<RoomOperationResult<RoomDetails>> CreateAsync(CreateRoomRequest? request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RoomRules.Authorize(user, true) is { } denied) return RoomRules.Denied<RoomDetails>(denied);
        if (request is null) return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InvalidInput, "room.request");
        try
        {
            var room = Room.Create(request.Name, request.Description, request.Location, request.Capacity, clock);
            if (await rooms.NameExistsAsync(room.Name, null, cancellationToken))
                return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.DuplicateName, "room.duplicateName");
            return RoomRules.Saved(room, await rooms.AddAsync(room, cancellationToken));
        }
        catch (DomainException error) { return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InvalidInput, error.Code); }
        catch (ReservationStorageException) { return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InfrastructureFailure, "room.storageFailure"); }
    }
}
