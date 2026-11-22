using Microsoft.AspNetCore.Identity;

namespace Roomify.Infrastructure.Identity;

/// <summary>Tożsamość techniczna pozostaje poza Domain; WP-00 przewiduje nazwę wyświetlaną i dezaktywację.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public ApplicationUser() : this(TimeProvider.System) { }

    public ApplicationUser(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        Id = Guid.NewGuid();
        CreatedAt = timeProvider.GetUtcNow().ToUniversalTime();
    }
}
