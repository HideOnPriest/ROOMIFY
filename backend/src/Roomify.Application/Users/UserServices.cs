using Roomify.Application.Abstractions;

namespace Roomify.Application.Users;

public sealed class CreateUserService(IUserAccountStore accounts, ICurrentUser user)
{
    public async Task<UserOperationResult<UserDetails>> CreateAsync(CreateUserRequest? request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UserRules.Authorize<UserDetails>(user) is { } denied) return denied;
        if (request is null || !UserRules.ValidEmail(request.Email) || string.IsNullOrWhiteSpace(request.InitialPassword))
            return UserOperationResult<UserDetails>.Failure(UserOperationStatus.InvalidInput, "user.input");
        return await accounts.CreateAsync(new(request.Email!.Trim(), request.DisplayName?.Trim(), request.InitialPassword), cancellationToken);
    }
}

public sealed class UpdateUserService(IUserAccountStore accounts, ICurrentUser user)
{
    public async Task<UserOperationResult<UserDetails>> UpdateAsync(UpdateUserRequest? request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UserRules.Authorize<UserDetails>(user) is { } denied) return denied;
        if (request is null || request.UserId == Guid.Empty || !UserRules.ValidEmail(request.Email))
            return UserOperationResult<UserDetails>.Failure(UserOperationStatus.InvalidInput, "user.input");
        return await accounts.UpdateAsync(request with { Email = request.Email!.Trim(), DisplayName = request.DisplayName?.Trim() }, cancellationToken);
    }
}

public sealed class GetUserByIdService(IUserAccountStore accounts, ICurrentUser user)
{
    public async Task<UserOperationResult<UserDetails>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UserRules.Authorize<UserDetails>(user) is { } denied) return denied;
        if (id == Guid.Empty) return UserOperationResult<UserDetails>.Failure(UserOperationStatus.InvalidInput, "user.id");
        return await accounts.FindAsync(id, cancellationToken);
    }
}

public sealed class GetUsersService(IUserAccountStore accounts, ICurrentUser user)
{
    public async Task<UserOperationResult<UserPage>> GetAsync(GetUsersRequest? request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UserRules.Authorize<UserPage>(user) is { } denied) return denied;
        if (request is null || !UserRules.Valid(request))
            return UserOperationResult<UserPage>.Failure(UserOperationStatus.InvalidInput, "user.query");
        return await accounts.SearchAsync(new((request.PageNumber - 1) * request.PageSize, request.PageSize,
            request.Search?.Trim(), request.IsActive, request.SortBy, request.SortDirection), cancellationToken);
    }
}

internal static class UserActivity
{
    internal static async Task<UserOperationResult<UserDetails>> ChangeAsync(IUserAccountStore accounts, ICurrentUser user,
        Guid id, bool active, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (UserRules.Authorize<UserDetails>(user) is { } denied) return denied;
        if (id == Guid.Empty) return UserOperationResult<UserDetails>.Failure(UserOperationStatus.InvalidInput, "user.id");
        return await accounts.SetActivityAsync(id, active, cancellationToken);
    }
}

public sealed class DeactivateUserService(IUserAccountStore accounts, ICurrentUser user)
{
    public Task<UserOperationResult<UserDetails>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
        => UserActivity.ChangeAsync(accounts, user, id, false, cancellationToken);
}

public sealed class ActivateUserService(IUserAccountStore accounts, ICurrentUser user)
{
    public Task<UserOperationResult<UserDetails>> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
        => UserActivity.ChangeAsync(accounts, user, id, true, cancellationToken);
}
