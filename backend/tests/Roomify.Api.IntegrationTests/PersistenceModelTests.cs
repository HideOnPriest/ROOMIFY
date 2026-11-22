using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "PersistenceMetadata")]
[Trait("RequiresDocker", "false")]
public sealed class PersistenceModelTests
{
    internal static RoomifyDbContext CreateContext() => new(new DbContextOptionsBuilder<RoomifyDbContext>()
        .UseNpgsql().Options);

    [Fact]
    public void Model_ContainsBusinessAndRequiredIdentityEntities()
    {
        using var context = CreateContext();
        IdentityModelAssertions.AssertExpectedEntityTypes(context.Model);
    }

    [Theory]
    [InlineData(typeof(Room), "rooms", "pk_rooms")]
    [InlineData(typeof(Reservation), "reservations", "pk_reservations")]
    public void Entities_HaveExpectedTableAndClientGeneratedGuidKey(Type type, string table, string keyName)
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(type)!;
        Assert.Equal(table, entity.GetTableName());
        var key = Assert.Single(entity.FindPrimaryKey()!.Properties);
        Assert.Equal("Id", key.Name);
        Assert.Equal(typeof(Guid), key.ClrType);
        Assert.Equal("id", key.GetColumnName());
        Assert.Equal(ValueGenerated.Never, key.ValueGenerated);
        Assert.Equal(keyName, entity.FindPrimaryKey()!.GetName());
    }

    [Theory]
    [InlineData(typeof(Room), "Name", "name", "character varying(100)", false, 100)]
    [InlineData(typeof(Room), "Location", "location", "character varying(150)", false, 150)]
    [InlineData(typeof(Room), "Description", "description", "text", true, null)]
    [InlineData(typeof(Room), "Capacity", "capacity", "integer", false, null)]
    [InlineData(typeof(Room), "IsActive", "is_active", "boolean", false, null)]
    [InlineData(typeof(Room), "CreatedAt", "created_at", "timestamp with time zone", false, null)]
    [InlineData(typeof(Reservation), "RoomId", "room_id", "uuid", false, null)]
    [InlineData(typeof(Reservation), "UserId", "user_id", "uuid", false, null)]
    [InlineData(typeof(Reservation), "Title", "title", "character varying(120)", false, 120)]
    [InlineData(typeof(Reservation), "Status", "status", "character varying(9)", false, 9)]
    [InlineData(typeof(Reservation), "StartAt", "start_at", "timestamp with time zone", false, null)]
    [InlineData(typeof(Reservation), "EndAt", "end_at", "timestamp with time zone", false, null)]
    [InlineData(typeof(Reservation), "CreatedAt", "created_at", "timestamp with time zone", false, null)]
    [InlineData(typeof(Reservation), "UpdatedAt", "updated_at", "timestamp with time zone", true, null)]
    public void Properties_HaveExpectedColumnTypesAndFacets(Type type, string property, string column,
        string sqlType, bool nullable, int? maxLength)
    {
        using var context = CreateContext();
        var metadata = context.Model.FindEntityType(type)!.FindProperty(property)!;
        Assert.Equal(column, metadata.GetColumnName());
        Assert.Equal(sqlType, metadata.GetColumnType());
        Assert.Equal(nullable, metadata.IsNullable);
        Assert.Equal(maxLength, metadata.GetMaxLength());
        Assert.False(metadata.IsShadowProperty());
    }

    [Fact]
    public void Period_IsRepresentedByTwoFieldsWithoutExtraEntityOrColumn()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Reservation))!;
        Assert.Null(entity.FindProperty("Period"));
        Assert.Empty(entity.GetComplexProperties());
        Assert.Equal("_startAt", entity.FindProperty("StartAt")!.FieldInfo!.Name);
        Assert.Equal("_endAt", entity.FindProperty("EndAt")!.FieldInfo!.Name);
        Assert.Equal(9, entity.GetProperties().Count(x => !x.IsShadowProperty()));
        var version = Assert.Single(entity.GetProperties(), x => x.IsShadowProperty());
        Assert.Equal("xmin", version.Name);
        Assert.Equal(typeof(uint), version.ClrType);
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
        Assert.Equal("xid", version.GetColumnType());
    }

    [Fact]
    public void Reservation_RelatesToRoomAndUserAndPreservesHistoryOnDelete()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Reservation))!;
        Assert.Equal(2, entity.GetForeignKeys().Count());
        var foreignKey = Assert.Single(entity.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(Room));
        Assert.Equal(typeof(Room), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal("RoomId", Assert.Single(foreignKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.False(foreignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Empty(entity.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Indexes_SupportRoomUserAndPeriodQueriesWithoutExclusionConstraint()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var reservation = model.FindEntityType(typeof(Reservation))!;
        var indexes = reservation.GetIndexes().ToDictionary(x => x.GetDatabaseName()!, x => x);
        Assert.Equal(3, indexes.Count);
        Assert.Equal(new[] { "RoomId", "StartAt", "EndAt" }, indexes["ix_reservations_room_period"].Properties.Select(x => x.Name));
        Assert.Equal(new[] { "UserId", "StartAt" }, indexes["ix_reservations_user_start"].Properties.Select(x => x.Name));
        Assert.Equal(new[] { "StartAt", "EndAt" }, indexes["ix_reservations_period"].Properties.Select(x => x.Name));
        Assert.All(indexes.Values, x => Assert.False(x.IsUnique));
        Assert.DoesNotContain(reservation.GetAnnotations(), x => x.Name.Contains("Exclusion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RoomName_HasUniqueCaseInsensitiveIndexIncludingArchivedRooms()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var room = model.FindEntityType(typeof(Room))!;
        var index = Assert.Single(room.GetIndexes());
        Assert.True(index.IsUnique);
        Assert.Equal("Name", Assert.Single(index.Properties).Name);
        Assert.Null(index.GetFilter());
        Assert.Equal("roomify_case_insensitive", room.FindProperty("Name")!.GetCollation());
        var collation = Assert.Single(model.GetCollations());
        Assert.Equal("icu", collation.Provider);
        Assert.Equal("und-u-ks-level2", collation.LcCollate);
        Assert.False(collation.IsDeterministic);
    }

    [Theory]
    [InlineData(ReservationStatus.Active, "active")]
    [InlineData(ReservationStatus.Cancelled, "cancelled")]
    public void Status_UsesExplicitStableStorageValues(ReservationStatus status, string storage)
    {
        using var context = CreateContext();
        var converter = context.Model.FindEntityType(typeof(Reservation))!.FindProperty("Status")!.GetValueConverter()!;
        Assert.Equal(storage, converter.ConvertToProvider(status));
        Assert.Equal(status, converter.ConvertFromProvider(storage));
    }

    [Fact]
    public void Status_RejectsUnknownValueInsteadOfSilentlyCancellingReservation()
    {
        using var context = CreateContext();
        var converter = context.Model.FindEntityType(typeof(Reservation))!.FindProperty("Status")!.GetValueConverter()!;
        Assert.Throws<InvalidOperationException>(() => converter.ConvertFromProvider("pending"));
        Assert.Throws<InvalidOperationException>(() => converter.ConvertToProvider((ReservationStatus)99));
    }

    [Fact]
    public void Query_TranslatesRoomAndHalfOpenPeriodFilterWithoutConnecting()
    {
        using var context = CreateContext();
        var from = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        var until = from.AddHours(1);
        var roomId = Guid.NewGuid();
        var sql = context.Reservations.Where(x => x.RoomId == roomId && x.StartAt < until && x.EndAt > from
            && x.Status == ReservationStatus.Active).ToQueryString();
        Assert.Contains("start_at <", sql);
        Assert.Contains("end_at >", sql);
        Assert.Contains("'active'", sql);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }
}
