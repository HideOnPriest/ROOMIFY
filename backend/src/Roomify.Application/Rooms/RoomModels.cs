using Roomify.Domain.Rooms;

namespace Roomify.Application.Rooms;

public sealed record CreateRoomRequest(string? Name, string? Description, string? Location, int Capacity);
public sealed record UpdateRoomRequest(Guid RoomId, string? Name, string? Description, string? Location, int Capacity);
public enum RoomSortBy { Name, Location, Capacity, CreatedAt }
public enum RoomSortDirection { Ascending, Descending }
public sealed record GetRoomsRequest(int PageNumber = 1, int PageSize = 20, string? Search = null,
    string? Location = null, int? MinCapacity = null, bool? IsActive = null,
    RoomSortBy SortBy = RoomSortBy.Name, RoomSortDirection SortDirection = RoomSortDirection.Ascending);

/// <summary>Zakres widoczności ustala Application przed przekazaniem zapytania do repozytorium.</summary>
public sealed record RoomSearch(int Offset, int Limit, string? Search, string? Location, int? MinCapacity,
    bool? IsActive, RoomSortBy SortBy, RoomSortDirection SortDirection);
public sealed record RoomDetails(Guid Id, string Name, string? Description, string Location, int Capacity,
    bool IsActive, DateTimeOffset CreatedAt)
{
    public static RoomDetails From(Room room) => new(room.Id, room.Name, room.Description, room.Location,
        room.Capacity, room.IsActive, room.CreatedAt);
}
public sealed record RoomPage(IReadOnlyList<RoomDetails> Items, int TotalCount, int PageNumber, int PageSize);
public enum RoomOperationStatus
{
    Success, InvalidInput, Unauthenticated, Forbidden, NotFound, DuplicateName,
    ConcurrentModification, AlreadyArchived, AlreadyActive, InfrastructureFailure
}
public sealed record RoomOperationResult<T>(RoomOperationStatus Status, T? Value, string? ErrorCode)
    where T : class
{
    internal static RoomOperationResult<T> Success(T value) => new(RoomOperationStatus.Success, value, null);
    internal static RoomOperationResult<T> Failure(RoomOperationStatus status, string code) => new(status, null, code);
}
public enum RoomWriteOutcome { Saved, DuplicateName, ConcurrentModification }
