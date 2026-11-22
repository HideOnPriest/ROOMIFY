using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Roomify.Application.Abstractions;

namespace Roomify.Infrastructure.Identity;

/// <summary>IsActive uczestniczy w standardowym sprawdzeniu hasła i warunków logowania Identity.</summary>
public sealed class ActiveAccountSignInManager(UserManager<ApplicationUser> users, IHttpContextAccessor accessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claims, IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger, IAuthenticationSchemeProvider schemes, IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(users, accessor, claims, options, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user) => user.IsActive && await base.CanSignInAsync(user);
}

/// <summary>Standardowa walidacja stempla i aktywności przy KAŻDYM uwierzytelnionym żądaniu.</summary>
public sealed class ActiveAccountStampValidator(IOptions<SecurityStampValidatorOptions> options,
    SignInManager<ApplicationUser> signIn, ILoggerFactory logger, IOptions<IdentityOptions> identityOptions)
    : SecurityStampValidator<ApplicationUser>(options, signIn, logger)
{
    public override async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        // Brak stempla nie jest starszą poprawną sesją. Odrzucamy również bilet bez rekordu konta.
        var hasStamp = context.Principal?.FindFirstValue(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType) is not null;
        var user = hasStamp ? await SignInManager.ValidateSecurityStampAsync(context.Principal) : null;
        if (user is { IsActive: true }) await SecurityStampVerified(user, context);
        else
        {
            context.RejectPrincipal();
            await SignInManager.SignOutAsync();
            await context.HttpContext.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);
        }
    }
}

public sealed class ActiveAccountClaimsFactory(UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles,
    IOptions<IdentityOptions> options) : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(users, roles, options)
{
    internal const string ActiveClaim = "roomify:active";
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        // Zarezerwowanego znacznika nie wolno nadpisać przechowywanym dowolnym claimem.
        foreach (var claim in identity.FindAll(ActiveClaim).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(ActiveClaim, user.IsActive ? "true" : "false"));
        return identity;
    }
}

/// <summary>Wyłącznie principal już zweryfikowany przez potok cookies, bez danych z request DTO.</summary>
public sealed class HttpIdentityCurrentUser(IHttpContextAccessor accessor, IOptions<IdentityOptions> options) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public Guid? UserId => Principal?.Identity?.IsAuthenticated == true
        && Guid.TryParse(Principal.FindFirstValue(options.Value.ClaimsIdentity.UserIdClaimType), out var id) && id != Guid.Empty ? id : null;
    public bool IsActive => UserId is not null && Principal?.FindFirstValue(ActiveAccountClaimsFactory.ActiveClaim) == "true";
    public bool IsAdministrator => IsActive && Principal!.IsInRole(IdentityRoles.Administrator);
}
