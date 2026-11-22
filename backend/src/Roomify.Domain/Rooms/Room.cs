using Roomify.Domain.Errors;

namespace Roomify.Domain.Rooms;

/// <summary>Sala zachowuje tożsamość i dane również po archiwizacji.</summary>
public sealed class Room
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Location { get; private set; }
    public int Capacity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private Room(string name, string? description, string location, int capacity, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Location = location;
        Capacity = capacity;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public static Room Create(string? name, string? description, string? location, int capacity,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var data = Validate(name, description, location, capacity);
        return new Room(data.Name, data.Description, data.Location, capacity, timeProvider.GetUtcNow());
    }

    public void UpdateDetails(string? name, string? description, string? location, int capacity)
    {
        // Najpierw walidujemy całość; nieudana zmiana nie może pozostawić częściowych danych.
        var data = Validate(name, description, location, capacity);
        Name = data.Name;
        Description = data.Description;
        Location = data.Location;
        Capacity = capacity;
    }

    public void Archive() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Walidacja przed sprawdzeniem unikalności, bez zmiany śledzonej encji.</summary>
    public void ValidateUpdate(string? name, string? description, string? location, int capacity)
        => Validate(name, description, location, capacity);

    private static (string Name, string? Description, string Location) Validate(
        string? name, string? description, string? location, int capacity)
    {
        var validName = DomainGuard.RequiredText(name, 100, "room.name");
        var validLocation = DomainGuard.RequiredText(location, 150, "room.location");
        if (capacity <= 0)
        {
            throw new DomainException(DomainErrorKind.InvalidInput, "room.capacity", "Pojemność musi być dodatnia.");
        }

        return (validName, string.IsNullOrWhiteSpace(description) ? null : description.Trim(), validLocation);
    }
}
