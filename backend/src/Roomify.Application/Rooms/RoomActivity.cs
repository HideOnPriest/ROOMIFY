using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;

namespace Roomify.Application.Rooms;

internal static class RoomActivity
{
    internal static async Task<RoomOperationResult<RoomDetails>> ChangeAsync(IRoomRepository rooms, ICurrentUser user,
        Guid id, bool active, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RoomRules.Authorize(user, true) is { } denied) return RoomRules.Denied<RoomDetails>(denied);
        if (id == Guid.Empty) return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InvalidInput, "room.id");
        try
        {
            var room = await rooms.FindForUpdateAsync(id, cancellationToken);
            if (room is null) return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.NotFound, "room.notFound");
            if (room.IsActive == active)
                return RoomOperationResult<RoomDetails>.Failure(active ? RoomOperationStatus.AlreadyActive : RoomOperationStatus.AlreadyArchived,
                    active ? "room.alreadyActive" : "room.alreadyArchived");
            if (active) room.Activate(); else room.Archive();
            return RoomRules.Saved(room, await rooms.SaveAsync(room, cancellationToken));
        }
        catch (ReservationStorageException) { return RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InfrastructureFailure, "room.storageFailure"); }
    }
}

public sealed class ArchiveRoomService(IRoomRepository rooms, ICurrentUser user)
{
    public Task<RoomOperationResult<RoomDetails>> ArchiveAsync(Guid roomId, CancellationToken cancellationToken = default)
        => RoomActivity.ChangeAsync(rooms, user, roomId, false, cancellationToken);
}

public sealed class ActivateRoomService(IRoomRepository rooms, ICurrentUser user)
{
    public Task<RoomOperationResult<RoomDetails>> ActivateAsync(Guid roomId, CancellationToken cancellationToken = default)
        => RoomActivity.ChangeAsync(rooms, user, roomId, true, cancellationToken);
}
