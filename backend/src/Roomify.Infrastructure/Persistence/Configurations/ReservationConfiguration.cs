using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;

namespace Roomify.Infrastructure.Persistence.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations", table =>
        {
            table.HasCheckConstraint("ck_reservations_period", "start_at < end_at");
            table.HasCheckConstraint("ck_reservations_status", "status IN ('active', 'cancelled')");
            table.HasCheckConstraint("ck_reservations_title_not_blank", "btrim(title) <> ''");
        });
        builder.HasKey(x => x.Id).HasName("pk_reservations");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        // Systemowy identyfikator wersji wiersza; Domain nie zna xmin ani EF.
        builder.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
        builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
        // Jawne literały nie zależą od kolejności wartości enum ani zmiany jego nazw.
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(9).IsRequired()
            .HasConversion(x => ReservationStatusStorage.ToStorage(x), x => ReservationStatusStorage.FromStorage(x));
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        // EF10 nie indeksuje skalarów ComplexProperty. Pola końców okresu umożliwiają
        // standardowe wiązanie konstruktora i indeksy bez dublowania stanu value object.
        builder.Ignore(x => x.Period);
        builder.Property(x => x.StartAt).HasField("_startAt").UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("start_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.EndAt).HasField("_endAt").UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("end_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_reservations_rooms_room_id");
        builder.HasIndex(x => new { x.RoomId, x.StartAt, x.EndAt }).HasDatabaseName("ix_reservations_room_period");
        builder.HasIndex(x => new { x.UserId, x.StartAt }).HasDatabaseName("ix_reservations_user_start");
        builder.HasIndex(x => new { x.StartAt, x.EndAt }).HasDatabaseName("ix_reservations_period");
    }
}
