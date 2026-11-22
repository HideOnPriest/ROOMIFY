using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;
using Roomify.Infrastructure.Identity;

namespace Roomify.Infrastructure.Persistence;

public sealed class RoomifyDbContext(DbContextOptions<RoomifyDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.HasCollation("roomify_case_insensitive", locale: "und-u-ks-level2", provider: "icu", deterministic: false);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoomifyDbContext).Assembly);
        IdentityModelConfiguration.Configure(modelBuilder);
    }
}
