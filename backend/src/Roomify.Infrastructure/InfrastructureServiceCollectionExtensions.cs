using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roomify.Application.Reservations;
using Roomify.Infrastructure.Persistence.Repositories;
using Roomify.Infrastructure.Persistence;
using Roomify.Infrastructure.Identity;

namespace Roomify.Infrastructure;

/// <summary>
/// Oddziela rejestracje implementacji technicznych od konfiguracji API
/// i od niezależnych od infrastruktury przypadków użycia Application.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        // Opcje są tworzone przy rozwiązaniu DbContext, bez otwierania połączenia.
        services.AddDbContext<RoomifyDbContext>(options => PostgreSqlConfiguration.Configure(options, configuration));
        services.TryAddScoped<IRoomRepository, EfRoomRepository>();
        services.TryAddScoped<IReservationRepository, EfReservationRepository>();
        return services.AddRoomifyIdentity(environment?.IsDevelopment() == true);
    }
}
