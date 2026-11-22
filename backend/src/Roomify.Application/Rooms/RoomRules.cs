using Roomify.Application.Abstractions;
using Roomify.Domain.Rooms;

namespace Roomify.Application.Rooms;

internal static class RoomRules
{
    internal static RoomOperationStatus? Authorize(ICurrentUser user, bool administrator)
    {
        return ApplicationAccess.Check(user, administrator) switch
        {
            AccessFailure.Unauthenticated => RoomOperationStatus.Unauthenticated,
            AccessFailure.Forbidden => RoomOperationStatus.Forbidden,
            _ => null
        };
    }

    internal static RoomOperationResult<T> Denied<T>(RoomOperationStatus status) where T : class
        => RoomOperationResult<T>.Failure(status, status == RoomOperationStatus.Unauthenticated ? "authentication.required" : "room.forbidden");

    internal static RoomOperationResult<RoomDetails> Saved(Room room, RoomWriteOutcome outcome) => outcome switch
    {
        RoomWriteOutcome.Saved => RoomOperationResult<RoomDetails>.Success(RoomDetails.From(room)),
        RoomWriteOutcome.DuplicateName => RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.DuplicateName, "room.duplicateName"),
        RoomWriteOutcome.ConcurrentModification => RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.ConcurrentModification, "room.concurrentModification"),
        _ => RoomOperationResult<RoomDetails>.Failure(RoomOperationStatus.InfrastructureFailure, "room.storageFailure")
    };

    internal static bool Valid(GetRoomsRequest request) => request.PageNumber >= 1
        && request.PageSize >= 1 && request.PageSize <= 100
        && ((long)request.PageNumber - 1) * request.PageSize <= int.MaxValue
        && (request.MinCapacity is null || request.MinCapacity > 0)
        && Enum.IsDefined(request.SortBy) && Enum.IsDefined(request.SortDirection);
}
