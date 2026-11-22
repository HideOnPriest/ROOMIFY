using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Roomify.Domain.Reservations;

namespace Roomify.Infrastructure.Identity;

internal static class IdentityModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().ToTable("users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        // Zmieniamy nazwy, zachowując typy, złożone klucze, concurrency i relacje bazowego modelu Identity.
        var types = new[] { typeof(ApplicationUser), typeof(IdentityRole<Guid>), typeof(IdentityUserRole<Guid>),
            typeof(IdentityUserClaim<Guid>), typeof(IdentityUserLogin<Guid>), typeof(IdentityUserToken<Guid>),
            typeof(IdentityRoleClaim<Guid>) };
        foreach (var type in types)
        {
            var entity = modelBuilder.Entity(type);
            var table = entity.Metadata.GetTableName()!;
            foreach (var property in entity.Metadata.GetProperties())
            {
                property.SetColumnName(SnakeCase(property.Name));
            }
            entity.HasKey(entity.Metadata.FindPrimaryKey()!.Properties.Select(x => x.Name).ToArray()).HasName("pk_" + table);
            foreach (var foreignKey in entity.Metadata.GetForeignKeys())
            {
                foreignKey.SetConstraintName($"fk_{table}_{foreignKey.PrincipalEntityType.GetTableName()}_{string.Join("_", foreignKey.Properties.Select(x => SnakeCase(x.Name)))}");
            }
        }

        var users = modelBuilder.Entity<ApplicationUser>();
        users.Property(x => x.Id).ValueGeneratedNever();
        users.Property(x => x.Email).IsRequired();
        users.Property(x => x.NormalizedEmail).IsRequired();
        users.Property(x => x.DisplayName).IsRequired(false);
        users.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        users.Property(x => x.IsActive).IsRequired();
        // BR-020: wzmacniamy istniejący EmailIndex, bez tworzenia drugiego indeksu.
        users.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("ux_users_normalized_email");
        users.HasIndex(x => x.NormalizedUserName).HasDatabaseName("ux_users_normalized_user_name");
        modelBuilder.Entity<IdentityRole<Guid>>().HasIndex(x => x.NormalizedName).HasDatabaseName("ux_roles_normalized_name");

        // Jawne długości utrzymują taki sam model także poza DI, np. w testach metadanych.
        users.Property(x => x.PhoneNumber).HasMaxLength(256);
        modelBuilder.Entity<IdentityUserLogin<Guid>>().Property(x => x.LoginProvider).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserLogin<Guid>>().Property(x => x.ProviderKey).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserToken<Guid>>().Property(x => x.LoginProvider).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserToken<Guid>>().Property(x => x.Name).HasMaxLength(128);

        modelBuilder.Entity<Reservation>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_reservations_users_user_id");
    }

    private static string SnakeCase(string name) => Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
