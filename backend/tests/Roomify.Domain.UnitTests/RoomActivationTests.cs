using Roomify.Domain.Errors;
using Roomify.Domain.Rooms;

namespace Roomify.Domain.UnitTests;

public sealed class RoomActivationTests
{
    [Fact]
    public void Activate_IsIdempotentAndPreservesIdentityAndDetails()
    {
        var room = Room.Create("Wisła", "Opis", "Piętro 1", 12, TimeProvider.System);
        var before = (room.Id, room.Name, room.Description, room.Location, room.Capacity, room.CreatedAt);
        room.Archive(); room.Activate(); room.Activate();
        Assert.True(room.IsActive);
        Assert.Equal(before, (room.Id, room.Name, room.Description, room.Location, room.Capacity, room.CreatedAt));
    }

    [Fact]
    public void ValidateUpdate_DoesNotMutateEvenWhenDataIsValid()
    {
        var room = Room.Create("Wisła", null, "Piętro 1", 12, TimeProvider.System);
        room.ValidateUpdate("Odra", "Nowy", "Piętro 2", 24);
        Assert.Equal("Wisła", room.Name); Assert.Null(room.Description); Assert.Equal("Piętro 1", room.Location); Assert.Equal(12, room.Capacity);
        Assert.Throws<DomainException>(() => room.ValidateUpdate("Odra", null, "Piętro 2", 0));
        Assert.Equal("Wisła", room.Name); Assert.Equal(12, room.Capacity);
    }
}
