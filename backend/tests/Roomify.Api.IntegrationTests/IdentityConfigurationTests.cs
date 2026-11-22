using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Roomify.Infrastructure.Identity;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Api.IntegrationTests;

[Trait("Category", "IdentityConfiguration")]
[Trait("RequiresDocker", "false")]
public sealed class IdentityConfigurationTests
{
    [Fact]
    public async Task Di_ResolvesManagersAndStandardStoresUsingOneScopedContextWithoutConnecting()
    {
        using var factory = new IdentityTestFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        Assert.NotNull(services.GetRequiredService<SignInManager<ApplicationUser>>());
        Assert.IsType<PasswordHasher<ApplicationUser>>(userManager.PasswordHasher);
        Assert.Contains(userManager.PasswordValidators, x => x is PasswordValidator<ApplicationUser>);
        Assert.Contains(userManager.UserValidators, x => x is UserValidator<ApplicationUser>);
        var context = services.GetRequiredService<RoomifyDbContext>();
        var userStore = Assert.IsAssignableFrom<UserStore<ApplicationUser, IdentityRole<Guid>, RoomifyDbContext, Guid,
            IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>, IdentityUserToken<Guid>,
            IdentityRoleClaim<Guid>, IdentityUserPasskey<Guid>>>(services.GetRequiredService<IUserStore<ApplicationUser>>());
        var roleStore = Assert.IsAssignableFrom<RoleStore<IdentityRole<Guid>, RoomifyDbContext, Guid,
            IdentityUserRole<Guid>, IdentityRoleClaim<Guid>>>(services.GetRequiredService<IRoleStore<IdentityRole<Guid>>>());
        Assert.Same(context, userStore.Context);
        Assert.Same(context, roleStore.Context);
        Assert.Same(context, Assert.Single(services.GetServices<RoomifyDbContext>()));
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        using var response = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Development", CookieSecurePolicy.SameAsRequest)]
    [InlineData("Testing", CookieSecurePolicy.Always)]
    [InlineData("Production", CookieSecurePolicy.Always)]
    public async Task CookieScheme_HasFiniteSecureHostOnlySession(string environment, CookieSecurePolicy securePolicy)
    {
        using var factory = new IdentityTestFactory(environment);
        var schemes = factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.Equal(IdentityConstants.ApplicationScheme, (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);
        Assert.Equal(IdentityConstants.ApplicationScheme, (await schemes.GetDefaultChallengeSchemeAsync())!.Name);
        Assert.Equal(typeof(CookieAuthenticationHandler), (await schemes.GetSchemeAsync(IdentityConstants.ApplicationScheme))!.HandlerType);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.Equal(securePolicy, options.Cookie.SecurePolicy);
        Assert.Null(options.Cookie.Domain);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Equal(TimeSpan.FromMinutes(30), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
        Assert.DoesNotContain(await schemes.GetAllSchemesAsync(), x => x.HandlerType.Name.Contains("Bearer") || x.HandlerType.Name.Contains("OAuth"));
    }

    [Fact]
    public void IdentityOptions_ConfigurePasswordLockoutAndUniqueEmailWithoutRegistration()
    {
        using var factory = new IdentityTestFactory();
        var options = factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.Equal(12, options.Password.RequiredLength);
        Assert.True(options.Password.RequireDigit);
        Assert.True(options.Password.RequireUppercase);
        Assert.True(options.Password.RequireLowercase);
        Assert.False(options.Password.RequireNonAlphanumeric);
        Assert.True(options.User.RequireUniqueEmail);
        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
        Assert.Equal(IdentitySchemaVersions.Version2, options.Stores.SchemaVersion);
        Assert.Equal(TimeSpan.Zero, factory.Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval);
    }

    [Theory]
    [InlineData("ExampleOnly123", true)]
    [InlineData("ExampleOnly123!", true)]
    [InlineData("Short123", false)]
    [InlineData("exampleonly123", false)]
    [InlineData("EXAMPLEONLY123", false)]
    [InlineData("ExampleOnlyLong", false)]
    public async Task ConfiguredPasswordPolicy_EnforcesSelectedRequirements(string password, bool valid)
    {
        using var factory = new IdentityTestFactory();
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await Assert.Single(manager.PasswordValidators).ValidateAsync(manager, new ApplicationUser(), password);
        Assert.Equal(valid, result.Succeeded);
    }

    [Fact]
    public async Task Http_UnsignedIdentityTicketIsRejectedWithoutRedirectAndKeepsSecureCookiePolicy()
    {
        using var factory = new IdentityTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false,
        });
        using var anonymous = await client.GetAsync("/__tests/identity/private");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(anonymous.Headers.Location);
        using var issued = await client.GetAsync("/__tests/identity/cookie");
        Assert.Equal(HttpStatusCode.NoContent, issued.StatusCode);
        var setCookie = Assert.Single(issued.Headers.GetValues("Set-Cookie"), x => x.StartsWith("Roomify.Auth="));
        Assert.Contains("httponly", setCookie.ToLowerInvariant());
        Assert.Contains("secure", setCookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
        var cookie = setCookie.Split(';')[0];
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var ticket = options.TicketDataFormat.Unprotect(cookie.Split('=', 2)[1]);
        Assert.NotNull(ticket);
        Assert.Equal(TimeSpan.FromMinutes(30), ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc);
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var authenticated = await client.GetAsync("/__tests/identity/private");
        Assert.Equal(HttpStatusCode.Unauthorized, authenticated.StatusCode);
        using var denied = await client.GetAsync("/__tests/identity/admin");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Null(denied.Headers.Location);
    }

    [Theory]
    [InlineData("/register")]
    [InlineData("/api/auth/register")]
    [InlineData("/Identity/Account/Register")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/logout")]
    [InlineData("/api/auth/me")]
    [InlineData("/login")]
    [InlineData("/logout")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/__tests/identity/private")]
    [InlineData("/__tests/identity/admin")]
    [InlineData("/__tests/identity/cookie")]
    public async Task ProductionHost_DoesNotExposeUnimplementedAuthOrIdentityUiEndpoints(string path)
    {
        using var factory = new RoomifyApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var get = await client.GetAsync(path);
        using var post = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }
}
