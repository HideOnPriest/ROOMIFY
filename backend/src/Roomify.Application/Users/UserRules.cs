using System.Net.Mail;
using Roomify.Application.Abstractions;

namespace Roomify.Application.Users;

internal static class UserRules
{
    internal static UserOperationResult<T>? Authorize<T>(ICurrentUser user) where T : class
        => ApplicationAccess.Check(user, true) switch
        {
            AccessFailure.Unauthenticated => UserOperationResult<T>.Failure(UserOperationStatus.Unauthenticated, "authentication.required"),
            AccessFailure.Forbidden => UserOperationResult<T>.Failure(UserOperationStatus.Forbidden, "user.forbidden"),
            _ => null
        };

    internal static bool ValidEmail(string? email) => email is not null && email.Trim().Length <= 256
        && MailAddress.TryCreate(email.Trim(), out var address)
        && string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);

    internal static bool Valid(GetUsersRequest request) => request.PageNumber >= 1
        && request.PageSize >= 1 && request.PageSize <= 100
        && ((long)request.PageNumber - 1) * request.PageSize <= int.MaxValue
        && Enum.IsDefined(request.SortBy) && Enum.IsDefined(request.SortDirection);
}
