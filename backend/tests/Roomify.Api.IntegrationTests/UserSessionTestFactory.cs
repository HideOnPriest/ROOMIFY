using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roomify.Application.Abstractions;
using Roomify.Application.Rooms;
using Roomify.Infrastructure.Identity;

namespace Roomify.Api.IntegrationTests;

internal sealed class UserSessionTestFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string,string?>
            { ["ConnectionStrings:Roomify"] = connectionString, ["HTTPS_PORT"] = null }));
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(AccountSessionProbeController).Assembly);
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }
}

/// <summary>Wyłącznie testowy host. Produkcyjny Program nie dołącza assembly tych kontrolerów.</summary>
[ApiController]
[Route("__tests/accounts")]
public sealed class AccountSessionProbeController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    ICurrentUser currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("issue/{id:guid}")]
    public async Task<IActionResult> Issue(Guid id)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null || !await signIn.CanSignInAsync(user)) return Unauthorized();
        await signIn.SignInAsync(user, false);
        return NoContent();
    }

    [Authorize]
    [HttpGet("private")]
    public IActionResult Private() => Ok(new { currentUser.UserId, currentUser.IsActive, currentUser.IsAdministrator });

    [Authorize(Roles = IdentityRoles.Administrator)]
    [HttpGet("admin")]
    public IActionResult Admin() => Ok();

    [Authorize]
    [HttpGet("rooms")]
    public async Task<IActionResult> Rooms([FromServices] GetRoomsService service)
        => Ok((await service.GetAsync(new())).Status);
}
