namespace Roomify.Application.Users;

/// <summary>Hasło jest wyłącznie wejściem; ToString nie ujawnia danych uwierzytelniających.</summary>
public sealed class CreateUserRequest(string? email, string? displayName, string? initialPassword)
{
    public string? Email { get; } = email;
    public string? DisplayName { get; } = displayName;
    public string? InitialPassword { get; } = initialPassword;
    public override string ToString() => nameof(CreateUserRequest);
}
public sealed record UpdateUserRequest(Guid UserId, string? Email, string? DisplayName);
public enum UserSortBy { Email, DisplayName, CreatedAt }
public enum UserSortDirection { Ascending, Descending }
public sealed record GetUsersRequest(int PageNumber = 1, int PageSize = 20, string? Search = null,
    bool? IsActive = null, UserSortBy SortBy = UserSortBy.Email, UserSortDirection SortDirection = UserSortDirection.Ascending);
public sealed record UserSearch(int Offset, int Limit, string? Search, bool? IsActive, UserSortBy SortBy, UserSortDirection SortDirection);
public sealed record UserDetails(Guid Id, string Email, string? DisplayName, bool IsActive, DateTimeOffset CreatedAt);
public sealed record UserPage(IReadOnlyList<UserDetails> Items, int TotalCount, int PageNumber, int PageSize);
public enum UserOperationStatus
{
    Success, InvalidInput, Unauthenticated, Forbidden, UserNotFound, DuplicateEmail,
    AlreadyInactive, AlreadyActive, ConcurrentModification, LastAdministrator, InfrastructureFailure
}
/// <summary>Jeden wynik dla wszystkich operacji kont, zgodny z konwencją sal i rezerwacji.</summary>
public sealed record UserOperationResult<T>(UserOperationStatus Status, T? Value, string? ErrorCode) where T : class
{
    public static UserOperationResult<T> Success(T value) => new(UserOperationStatus.Success, value, null);
    public static UserOperationResult<T> Failure(UserOperationStatus status, string code) => new(status, null, code);
}

public interface IUserAccountStore
{
    Task<UserOperationResult<UserDetails>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserOperationResult<UserDetails>> UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken);
    Task<UserOperationResult<UserDetails>> SetActivityAsync(Guid id, bool active, CancellationToken cancellationToken);
    Task<UserOperationResult<UserDetails>> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<UserOperationResult<UserPage>> SearchAsync(UserSearch query, CancellationToken cancellationToken);
}
