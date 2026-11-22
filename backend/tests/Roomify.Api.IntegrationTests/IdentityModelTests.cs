using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Roomify.Domain.Reservations;
using Roomify.Infrastructure.Identity;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "IdentityConfiguration")]
[Trait("RequiresDocker", "false")]
public sealed class IdentityModelTests
{
    [Theory]
    [InlineData(typeof(ApplicationUser), "users", "Id")]
    [InlineData(typeof(IdentityRole<Guid>), "roles", "Id")]
    [InlineData(typeof(IdentityUserRole<Guid>), "user_roles", "UserId,RoleId")]
    [InlineData(typeof(IdentityUserClaim<Guid>), "user_claims", "Id")]
    [InlineData(typeof(IdentityUserLogin<Guid>), "user_logins", "LoginProvider,ProviderKey")]
    [InlineData(typeof(IdentityUserToken<Guid>), "user_tokens", "UserId,LoginProvider,Name")]
    [InlineData(typeof(IdentityRoleClaim<Guid>), "role_claims", "Id")]
    public void IdentityEntities_PreserveKeysAndUseSnakeCaseColumns(Type type, string table, string keys)
    {
        using var context = PersistenceModelTests.CreateContext();
        var entity = context.Model.FindEntityType(type)!;
        Assert.Equal(table, entity.GetTableName());
        Assert.Equal(keys.Split(','), entity.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.All(entity.GetProperties(), x => Assert.Matches("^[a-z][a-z0-9_]*$", x.GetColumnName()!));
        if (type == typeof(ApplicationUser) || type == typeof(IdentityRole<Guid>))
        {
            Assert.Equal(typeof(Guid), entity.FindPrimaryKey()!.Properties.Single().ClrType);
        }
        if (type == typeof(IdentityUserClaim<Guid>) || type == typeof(IdentityRoleClaim<Guid>))
        {
            Assert.Equal(typeof(int), entity.FindPrimaryKey()!.Properties.Single().ClrType);
        }
    }

    [Fact]
    public void SharedContext_IncludesAllExpectedTypesAndRestrictsUserDeletion()
    {
        using var context = PersistenceModelTests.CreateContext();
        Assert.IsAssignableFrom<IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>>(context);
        IdentityModelAssertions.AssertExpectedEntityTypes(context.Model);
        var reservation = context.Model.FindEntityType(typeof(Reservation))!;
        var key = Assert.Single(reservation.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        Assert.Equal("UserId", Assert.Single(key.Properties).Name);
        Assert.True(key.IsRequired);
        Assert.False(key.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior);
        Assert.Empty(reservation.GetNavigations());
        Assert.Equal("reservations", reservation.GetTableName());
        Assert.Equal("timestamp with time zone", reservation.FindProperty("StartAt")!.GetColumnType());
    }

    [Fact]
    public void SecurityPropertiesAndIndexes_AreRetainedWithoutExtraRequiredProfileFields()
    {
        using var context = PersistenceModelTests.CreateContext();
        var user = context.Model.FindEntityType(typeof(ApplicationUser))!;
        Assert.True(user.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken);
        foreach (var property in new[] { "PasswordHash", "SecurityStamp", "NormalizedUserName", "NormalizedEmail",
                     "LockoutEnd", "LockoutEnabled", "AccessFailedCount", "TwoFactorEnabled" })
        {
            Assert.NotNull(user.FindProperty(property));
        }
        Assert.Null(user.FindProperty("FirstName"));
        Assert.Null(user.FindProperty("LastName"));
        Assert.True(user.FindProperty("DisplayName")!.IsNullable);
        Assert.False(user.FindProperty("Email")!.IsNullable);
        Assert.False(user.FindProperty("NormalizedEmail")!.IsNullable);
        Assert.False(user.FindProperty("IsActive")!.IsNullable);
        Assert.Equal("timestamp with time zone", user.FindProperty("CreatedAt")!.GetColumnType());
        Assert.Equal(2, user.GetIndexes().Count());
        Assert.True(Assert.Single(user.GetIndexes(), x => x.Properties.Single().Name == "NormalizedEmail").IsUnique);
        Assert.True(Assert.Single(user.GetIndexes(), x => x.Properties.Single().Name == "NormalizedUserName").IsUnique);
        var role = context.Model.FindEntityType(typeof(IdentityRole<Guid>))!;
        Assert.True(role.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken);
        Assert.True(Assert.Single(role.GetIndexes()).IsUnique);
    }

    [Fact]
    public void UserInitialization_UsesExplicitClockWithoutAddingCustomPasswordStorage()
    {
        var now = new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.FromHours(1));
        var user = new ApplicationUser(new FixedClock(now));
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.True(user.IsActive);
        Assert.Null(user.DisplayName);
        Assert.Equal(now.ToUniversalTime(), user.CreatedAt);
        Assert.Equal(TimeSpan.Zero, user.CreatedAt.Offset);
        Assert.Null(user.PasswordHash);
        Assert.Throws<ArgumentNullException>(() => new ApplicationUser(null!));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
