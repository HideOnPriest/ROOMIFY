using Microsoft.Extensions.DependencyInjection;

namespace Roomify.Infrastructure;

/// <summary>
/// Oddziela rejestracje implementacji technicznych od konfiguracji API
/// i od niezależnych od infrastruktury przypadków użycia Application.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Dostęp do danych i Identity nie są częścią szkieletu WP-01.2.
        return services;
    }
}
