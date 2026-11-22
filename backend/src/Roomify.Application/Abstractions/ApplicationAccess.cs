namespace Roomify.Application.Abstractions;

public enum AccessFailure { Unauthenticated, Forbidden }

/// <summary>Wspólna granica dostępu dla wszystkich przypadków użycia.</summary>
public static class ApplicationAccess
{
    public static AccessFailure? Check(ICurrentUser user, bool administrator = false)
    {
        if (user.UserId is null || user.UserId == Guid.Empty) return AccessFailure.Unauthenticated;
        if (!user.IsActive || (administrator && !user.IsAdministrator)) return AccessFailure.Forbidden;
        return null;
    }
}
