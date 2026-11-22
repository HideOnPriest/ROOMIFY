using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roomify.Domain.Rooms;

namespace Roomify.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms", table =>
        {
            table.HasCheckConstraint("ck_rooms_name_not_blank", "btrim(name) <> ''");
            table.HasCheckConstraint("ck_rooms_location_not_blank", "btrim(location) <> ''");
            table.HasCheckConstraint("ck_rooms_capacity_positive", "capacity > 0");
        });
        builder.HasKey(x => x.Id).HasName("pk_rooms");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Capacity).HasColumnName("capacity").IsRequired();
        builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        // BR-025 wymaga unikalności bez rozróżniania wielkości liter.
        builder.Property(x => x.Name).UseCollation("roomify_case_insensitive");
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_rooms_name");
    }
}
