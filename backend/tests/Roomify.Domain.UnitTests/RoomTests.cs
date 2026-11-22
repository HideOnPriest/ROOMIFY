using Roomify.Domain.Errors;
using Roomify.Domain.Rooms;

namespace Roomify.Domain.UnitTests;

[Trait("Rules", "BR-025, BR-019; FR-ROOM-004/005/006")]
public sealed class RoomTests
{
    [Fact]
    public void Create_NormalizesDetailsAndCreatesActiveRoom()
    {
        var clock = DomainTestData.Clock();
        var room = Room.Create("  Wisła  ", "  Rzutnik  ", "  Piętro 1  ", 12, clock);

        Assert.NotEqual(Guid.Empty, room.Id);
        Assert.Equal("Wisła", room.Name);
        Assert.Equal("Rzutnik", room.Description);
        Assert.Equal("Piętro 1", room.Location);
        Assert.Equal(12, room.Capacity);
        Assert.True(room.IsActive);
        Assert.Equal(clock.UtcNow, room.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void Create_RejectsMissingName(string? name)
    {
        var error = Assert.Throws<DomainException>(() => Room.Create(name, null, "Piętro 1", 1, DomainTestData.Clock()));
        Assert.Equal(DomainErrorKind.InvalidInput, error.Kind);
        Assert.Equal("room.name", error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void Create_RejectsMissingLocation(string? location)
    {
        var error = Assert.Throws<DomainException>(() => Room.Create("Sala", null, location, 1, DomainTestData.Clock()));
        Assert.Equal(DomainErrorKind.InvalidInput, error.Kind);
        Assert.Equal("room.location", error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_RejectsNonPositiveCapacity(int capacity)
    {
        var error = Assert.Throws<DomainException>(() => Room.Create("Sala", null, "Piętro 1", capacity, DomainTestData.Clock()));
        Assert.Equal(DomainErrorKind.InvalidInput, error.Kind);
        Assert.Equal("room.capacity", error.Code);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(100, 150, int.MaxValue)]
    public void Create_AcceptsInclusiveTextAndCapacityBounds(int nameLength, int locationLength, int capacity)
    {
        var room = Room.Create(" " + new string('N', nameLength) + " ", null,
            " " + new string('L', locationLength) + " ", capacity, DomainTestData.Clock());
        Assert.Equal(new string('N', nameLength), room.Name);
        Assert.Equal(new string('L', locationLength), room.Location);
        Assert.Equal(capacity, room.Capacity);
    }

    [Theory]
    [InlineData(101, 150, "room.name")]
    [InlineData(100, 151, "room.location")]
    public void Create_RejectsTextAboveBounds(int nameLength, int locationLength, string code)
    {
        var error = Assert.Throws<DomainException>(() => Room.Create(new string('N', nameLength), null,
            new string('L', locationLength), 1, DomainTestData.Clock()));
        Assert.Equal(DomainErrorKind.InvalidInput, error.Kind);
        Assert.Equal(code, error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_AllowsOptionalDescription(string? description)
    {
        var room = Room.Create("Sala", description, "Piętro 1", 1, DomainTestData.Clock());
        Assert.Null(room.Description);
    }

    [Fact]
    public void UpdateDetails_ChangesDataAndPreservesIdentityAndCreationTime()
    {
        var room = Room.Create("Sala", "Stary opis", "Piętro 1", 1, DomainTestData.Clock());
        var id = room.Id;
        var created = room.CreatedAt;
        room.UpdateDetails("  Odra ", "  Nowy opis ", " Piętro 2 ", 20);

        Assert.Equal(id, room.Id);
        Assert.Equal(created, room.CreatedAt);
        Assert.Equal("Odra", room.Name);
        Assert.Equal("Nowy opis", room.Description);
        Assert.Equal("Piętro 2", room.Location);
        Assert.Equal(20, room.Capacity);
        Assert.True(room.IsActive);
    }

    [Theory]
    [InlineData("", "Nowa", 5, "room.name")]
    [InlineData("Nowa", "", 5, "room.location")]
    [InlineData("Nowa", "Nowa", 0, "room.capacity")]
    public void UpdateDetails_InvalidDataDoesNotPartiallyChangeRoom(string name, string location, int capacity, string code)
    {
        var room = Room.Create("Sala", "Opis", "Piętro 1", 1, DomainTestData.Clock());
        var error = Assert.Throws<DomainException>(() => room.UpdateDetails(name, "Nowy opis", location, capacity));
        Assert.Equal(code, error.Code);
        Assert.Equal("Sala", room.Name);
        Assert.Equal("Opis", room.Description);
        Assert.Equal("Piętro 1", room.Location);
        Assert.Equal(1, room.Capacity);
    }

    [Fact]
    public void Archive_IsIdempotentAndKeepsDataEditableWithoutReactivation()
    {
        var room = Room.Create("Sala", "Opis", "Piętro 1", 1, DomainTestData.Clock());
        var id = room.Id;
        var created = room.CreatedAt;
        room.Archive();
        room.Archive();
        Assert.False(room.IsActive);
        Assert.Equal("Sala", room.Name);
        Assert.Equal("Opis", room.Description);
        Assert.Equal("Piętro 1", room.Location);
        Assert.Equal(1, room.Capacity);

        // WP-00 nie zakazuje edycji opisów zarchiwizowanej sali.
        room.UpdateDetails("Nowa nazwa", null, "Piętro 2", 2);
        Assert.False(room.IsActive);
        Assert.Null(room.Description);
        Assert.Equal("Nowa nazwa", room.Name);
        Assert.Equal(id, room.Id);
        Assert.Equal(created, room.CreatedAt);
    }
}
