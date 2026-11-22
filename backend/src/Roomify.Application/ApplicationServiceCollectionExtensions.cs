using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roomify.Application.Abstractions;
using Roomify.Application.Reservations;
using Roomify.Application.Rooms;
using Roomify.Application.Users;

namespace Roomify.Application;

/// <summary>
/// Wspólny punkt rejestracji przypadków użycia: API nie będzie musiało znać
/// szczegółów tworzenia usług należących do tej warstwy.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddScoped<CreateReservationService>();
        services.TryAddScoped<UpdateReservationService>();
        services.TryAddScoped<CancelReservationService>();
        services.TryAddScoped<CreateRoomService>();
        services.TryAddScoped<UpdateRoomService>();
        services.TryAddScoped<ArchiveRoomService>();
        services.TryAddScoped<ActivateRoomService>();
        services.TryAddScoped<GetRoomByIdService>();
        services.TryAddScoped<GetRoomsService>();
        services.TryAddScoped<CreateUserService>();
        services.TryAddScoped<UpdateUserService>();
        services.TryAddScoped<DeactivateUserService>();
        services.TryAddScoped<ActivateUserService>();
        services.TryAddScoped<GetUserByIdService>();
        services.TryAddScoped<GetUsersService>();
        services.TryAddScoped<ICurrentUser, UnauthenticatedCurrentUser>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        return services;
    }

    /// <summary>Adapter techniczny zastępuje tylko domyślną odmowę; jawny adapter hosta/testu pozostaje.</summary>
    public static IServiceCollection AddCurrentUserAdapter<T>(this IServiceCollection services) where T : class, ICurrentUser
    {
        var registration = services.LastOrDefault(x => x.ServiceType == typeof(ICurrentUser));
        if (registration?.ImplementationType == typeof(UnauthenticatedCurrentUser)) services.Remove(registration);
        services.TryAddScoped<ICurrentUser, T>();
        return services;
    }
}
