using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roomify.Application;
using Roomify.Application.Users;
using Roomify.Infrastructure.Persistence;

namespace Roomify.Infrastructure.Identity;

internal static class IdentityServiceCollectionExtensions
{
    internal static IServiceCollection AddRoomifyIdentity(this IServiceCollection services, bool isDevelopment)
    {
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            // Długie hasła mogą zawierać znaki specjalne, ale nie muszą.
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            // Zakres obejmuje hasła/cookies i siedem klasycznych typów Identity, bez passkeys.
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
            options.Stores.MaxLengthForKeys = 128;
        }).AddEntityFrameworkStores<RoomifyDbContext>()
            .AddSignInManager<ActiveAccountSignInManager>()
            .AddClaimsPrincipalFactory<ActiveAccountClaimsFactory>();

        services.Replace(ServiceDescriptor.Scoped<ISecurityStampValidator, ActiveAccountStampValidator>());
        services.AddCurrentUserAdapter<HttpIdentityCurrentUser>();
        services.TryAddScoped<IUserAccountStore, IdentityUserAccountStore>();

        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Roomify.Auth";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            // API nie ma Razor login/access-denied. Statusy są jawne także poza metadanymi API z .NET 10.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        return services;
    }
}
