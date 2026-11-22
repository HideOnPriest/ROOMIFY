using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;

namespace Roomify.Application.Rooms;

public sealed class GetRoomByIdService(IRoomRepository rooms, ICurrentUser user)
{
    public async Task<RoomOperationResult<RoomDetails>> GetAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RoomRules.Authorize(user, false) is { } denied) return RoomRules.Denied<RoomDetails>(denied);
        if (roomId == Guid.Empty) return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InvalidInput, "room.id");
        try
        {
            var room = await rooms.FindByIdAsync(roomId, cancellationToken);
            if (room is null || (!room.IsActive && !user.IsAdministrator))
                return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.NotFound, "room.notFound");
            return RoomOperationResult<RoomDetails>.Success(RoomDetails.From(room));
        }
        catch (ReservationStorageException) { return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InfrastructureFailure, "room.storageFailure"); }
    }
}
