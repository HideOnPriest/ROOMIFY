using Microsoft.Extensions.DependencyInjection;

namespace Roomify.Application;

/// <summary>
/// Wspólny punkt rejestracji przypadków użycia: API nie będzie musiało znać
/// szczegółów tworzenia usług należących do tej warstwy.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Rejestracje powstaną wraz z rzeczywistymi przypadkami użycia.
        return services;
    }
}
