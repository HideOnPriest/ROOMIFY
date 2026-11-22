using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roomify.Infrastructure.Identity;

namespace Roomify.Api.IntegrationTests;

internal sealed class IdentityTestFactory(string environment = "Testing") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["HTTPS_PORT"] = null,
                ["ConnectionStrings:Roomify"] = "Host=127.0.0.1;Port=1;Database=identity_configuration_test;Username=metadata_test;Timeout=1",
            }));
        builder.ConfigureTestServices(services =>
        {
            // Kontroler występuje wyłącznie w assembly testów i jest dołączany wyłącznie przez tę fixture.
            services.AddControllers().AddApplicationPart(typeof(IdentityProbeController).Assembly);
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }
}

/// <summary>Próby potoku HTTP bez kont w bazie; żaden endpoint nie trafia do produkcyjnego API.</summary>
[ApiController]
[Route("__tests/identity")]
public sealed class IdentityProbeController : ControllerBase
{
    [HttpGet("private")]
    [Authorize]
    public IActionResult Private() => Ok();

    [HttpGet("admin")]
    [Authorize(Roles = IdentityRoles.Administrator)]
    public IActionResult Admin() => Ok();

    [HttpGet("cookie")]
    [AllowAnonymous]
    public async Task<IActionResult> Cookie()
    {
        // Bilet testowy nie tworzy użytkownika ani sesji przez SignInManager/EF.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, IdentityRoles.User),
        }, IdentityConstants.ApplicationScheme));
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);
        return NoContent();
    }
}
