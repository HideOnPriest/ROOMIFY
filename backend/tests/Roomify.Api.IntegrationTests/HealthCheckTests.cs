using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Roomify.Api.IntegrationTests;

// xUnit udostępnia jedną fixture na klasę i zwalnia ją po zakończeniu testów.
// Trait pozwala wybrać wyłącznie testy bez Dockera, zanim inne fixture wystartują.
[Trait("Category", "Integration")]
[Trait("RequiresDocker", "false")]
public sealed class HealthCheckTests(RoomifyApiFactory factory)
    : IClassFixture<RoomifyApiFactory>
{
    [Fact(DisplayName = "GET /health zwraca 200 oraz tekst Healthy bez bazy danych")]
    public async Task Health_ReturnsOkAndHealthy()
    {
        // ARRANGE — klient HttpClient wysyła żądania do TestServer w pamięci.
        // To test integracyjny: współpracują host, DI, routing i Health Checks.
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // ACT — wykonujemy rzeczywisty potok HTTP, bez PostgreSQL i sieci.
        using var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // ASSERT — sam brak wyjątku nie wystarcza: sprawdzamy kontrakt odpowiedzi.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", content);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    // Fact oznacza pojedynczy przypadek; Theory powtarza test dla danych InlineData.
    [Theory(DisplayName = "Nieistniejący endpoint zwraca 404 zamiast przekierowania")]
    [InlineData("/nieistniejacy-endpoint")]
    [InlineData("/api/nieistniejacy-endpoint")]
    public async Task UnknownEndpoint_ReturnsNotFound(string path)
    {
        // ARRANGE — wyłączamy przekierowania, aby nie ukrywały odpowiedzi API.
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // ACT — sprawdzamy trasę techniczną i przyszły prefiks biznesowy.
        using var response = await client.GetAsync(path);

        // ASSERT — API nie serwuje dokumentu startowego Angulara.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
