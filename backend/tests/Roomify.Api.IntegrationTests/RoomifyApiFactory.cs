using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Roomify.Api.IntegrationTests;

/// <summary>
/// WebApplicationFactory składa rzeczywisty host API, routing i DI, ale obsługuje
/// żądania przez TestServer w pamięci zamiast przez sieciowy port Kestrela.
/// </summary>
public sealed class RoomifyApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Test nie zależy od profilu HTTPS ani ustawień portu na komputerze autora.
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HTTPS_PORT"] = null,
            }));
    }
}
