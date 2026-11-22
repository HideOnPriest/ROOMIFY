using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roomify.Application.Users;
using Roomify.Infrastructure.Persistence;
using Roomify.Infrastructure.Persistence.Repositories;

namespace Roomify.Infrastructure.Identity;

/// <summary>Jeden kontekst Identity, transakcja obejmuje wszystkie zapisy managerów danej operacji.</summary>
public sealed class IdentityUserAccountStore(RoomifyDbContext context, UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles, TimeProvider clock) : IUserAccountStore
{
    private const long AdministratorLock = 7810934001;

    public Task<UserOperationResult<UserDetails>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
        => WriteAsync(async () =>
        {
            if (await users.FindByEmailAsync(request.Email!) is not null) return Failure(UserOperationStatus.DuplicateEmail, "user.duplicateEmail");
            var role = await roles.FindByNameAsync(IdentityRoles.User);
            if (role is null)
            {
                var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(IdentityRoles.User) { Id = Guid.NewGuid() });
                if (!roleResult.Succeeded) return Failure(UserOperationStatus.InfrastructureFailure, "user.roleUnavailable");
            }
            var user = new ApplicationUser(clock) { UserName = request.Email, Email = request.Email,
                DisplayName = NormalizeDisplayName(request.DisplayName), IsActive = true };
            var created = await users.CreateAsync(user, request.InitialPassword!);
            if (!created.Succeeded) return FromIdentity(created);
            var assigned = await users.AddToRoleAsync(user, IdentityRoles.User);
            if (!assigned.Succeeded) return Failure(UserOperationStatus.InfrastructureFailure, "user.roleAssignmentFailed");
            return Success(user);
        }, cancellationToken);

    public Task<UserOperationResult<UserDetails>> UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken)
        => WriteAsync(async () =>
        {
            var user = await users.FindByIdAsync(request.UserId.ToString());
            if (user is null) return Failure(UserOperationStatus.UserNotFound, "user.notFound");
            var duplicate = await users.FindByEmailAsync(request.Email!);
            if (duplicate is not null && duplicate.Id != user.Id) return Failure(UserOperationStatus.DuplicateEmail, "user.duplicateEmail");
            var email = await users.SetEmailAsync(user, request.Email);
            if (!email.Succeeded) return FromIdentity(email);
            var login = await users.SetUserNameAsync(user, request.Email);
            if (!login.Succeeded) return FromIdentity(login);
            user.DisplayName = NormalizeDisplayName(request.DisplayName);
            var saved = await users.UpdateSecurityStampAsync(user);
            return saved.Succeeded ? Success(user) : FromIdentity(saved);
        }, cancellationToken, serialize: false);

    public Task<UserOperationResult<UserDetails>> SetActivityAsync(Guid id, bool active, CancellationToken cancellationToken)
        => WriteAsync(async () =>
        {
            var user = await users.FindByIdAsync(id.ToString());
            if (user is null) return Failure(UserOperationStatus.UserNotFound, "user.notFound");
            if (user.IsActive == active) return Failure(active ? UserOperationStatus.AlreadyActive : UserOperationStatus.AlreadyInactive,
                active ? "user.alreadyActive" : "user.alreadyInactive");
            if (!active && await users.IsInRoleAsync(user, IdentityRoles.Administrator))
            {
                var administrator = roles.NormalizeKey(IdentityRoles.Administrator);
                var count = await (from account in context.Users.AsNoTracking()
                    join assignment in context.UserRoles on account.Id equals assignment.UserId
                    join role in context.Roles on assignment.RoleId equals role.Id
                    where account.IsActive && role.NormalizedName == administrator select account.Id).CountAsync(cancellationToken);
                if (count <= 1) return Failure(UserOperationStatus.LastAdministrator, "user.lastAdministrator");
            }
            user.IsActive = active;
            // Zmiana IsActive i stempla jest jednym zapisem Identity. Aktywacja nie przywraca starych sesji.
            var saved = await users.UpdateSecurityStampAsync(user);
            return saved.Succeeded ? Success(user) : FromIdentity(saved);
        }, cancellationToken);

    public async Task<UserOperationResult<UserDetails>> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await context.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new UserDetails(x.Id, x.Email!, x.DisplayName, x.IsActive, x.CreatedAt)).SingleOrDefaultAsync(cancellationToken);
            return user is null ? Failure(UserOperationStatus.UserNotFound, "user.notFound") : UserOperationResult<UserDetails>.Success(user);
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error)) { return Failure(UserOperationStatus.InfrastructureFailure, "user.storageFailure"); }
    }

    public async Task<UserOperationResult<UserPage>> SearchAsync(UserSearch query, CancellationToken cancellationToken)
    {
        try
        {
            var source = context.Users.AsNoTracking();
            if (query.IsActive is { } active) source = source.Where(x => x.IsActive == active);
            if (!string.IsNullOrEmpty(query.Search))
            {
                var pattern = "%" + query.Search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                source = source.Where(x => EF.Functions.ILike(x.Email!, pattern, "\\") || (x.DisplayName != null && EF.Functions.ILike(x.DisplayName, pattern, "\\")));
            }
            var count = await source.CountAsync(cancellationToken);
            var descending = query.SortDirection == UserSortDirection.Descending;
            var sorted = query.SortBy switch
            {
                UserSortBy.Email => descending ? source.OrderByDescending(x => x.Email) : source.OrderBy(x => x.Email),
                UserSortBy.DisplayName => descending ? source.OrderByDescending(x => x.DisplayName) : source.OrderBy(x => x.DisplayName),
                UserSortBy.CreatedAt => descending ? source.OrderByDescending(x => x.CreatedAt) : source.OrderBy(x => x.CreatedAt),
                _ => throw new ArgumentOutOfRangeException(nameof(query))
            };
            var items = await sorted.ThenBy(x => x.Id).Skip(query.Offset).Take(query.Limit)
                .Select(x => new UserDetails(x.Id, x.Email!, x.DisplayName, x.IsActive, x.CreatedAt)).ToListAsync(cancellationToken);
            return UserOperationResult<UserPage>.Success(new(items, count, query.Offset / query.Limit + 1, query.Limit));
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error)) { return UserOperationResult<UserPage>.Failure(UserOperationStatus.InfrastructureFailure, "user.storageFailure"); }
    }

    private async Task<UserOperationResult<UserDetails>> WriteAsync(Func<Task<UserOperationResult<UserDetails>>> operation,
        CancellationToken cancellationToken, bool serialize = true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // UserStore wywołuje SaveChanges. Operacja nie może niejawnie zapisać cudzych oczekujących zmian.
        if (context.ChangeTracker.HasChanges()) throw new InvalidOperationException("Operacja konta wymaga zakresu bez oczekujących zmian.");
        var existing = context.ChangeTracker.Entries().Select(x => x.Entity).ToHashSet();
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            // Tworzenie roli i zmiany aktywności są serializowane. Edycje profilu chroni ConcurrencyStamp Identity.
            if (serialize) await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({AdministratorLock})", cancellationToken);
            var result = await operation();
            if (result.Status == UserOperationStatus.Success)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
                DetachOperation(existing);
            }
            return result;
        }
        catch (DbUpdateConcurrencyException) { DetachOperation(existing); return Failure(UserOperationStatus.ConcurrentModification, "user.concurrentModification"); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_users_normalized_email" or "ux_users_normalized_user_name" })
        { DetachOperation(existing); return Failure(UserOperationStatus.DuplicateEmail, "user.duplicateEmail"); }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error))
        { DetachOperation(existing); return Failure(UserOperationStatus.InfrastructureFailure, "user.storageFailure"); }
        catch (OperationCanceledException) { DetachOperation(existing); throw; }
    }

    private void DetachOperation(HashSet<object> existing)
    {
        foreach (var entry in context.ChangeTracker.Entries().ToArray())
            if (!existing.Contains(entry.Entity) || entry.Entity is ApplicationUser) entry.State = EntityState.Detached;
    }

    private static string? NormalizeDisplayName(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    private static UserOperationResult<UserDetails> Success(ApplicationUser user) => UserOperationResult<UserDetails>.Success(new(user.Id, user.Email!, user.DisplayName, user.IsActive, user.CreatedAt));
    private static UserOperationResult<UserDetails> Failure(UserOperationStatus status, string code) => UserOperationResult<UserDetails>.Failure(status, code);
    private static UserOperationResult<UserDetails> FromIdentity(IdentityResult result)
    {
        var codes = result.Errors.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        if (codes.Contains("ConcurrencyFailure")) return Failure(UserOperationStatus.ConcurrentModification, "user.concurrentModification");
        if (codes.Contains("DuplicateEmail") || codes.Contains("DuplicateUserName")) return Failure(UserOperationStatus.DuplicateEmail, "user.duplicateEmail");
        if (codes.All(x => x.StartsWith("Password", StringComparison.Ordinal) || x is "InvalidEmail" or "InvalidUserName"))
            return Failure(UserOperationStatus.InvalidInput, "user.identityValidation");
        return Failure(UserOperationStatus.InfrastructureFailure, "user.storageFailure");
    }
}
