using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TireScheduler.Api.Modules.Auth;

// Puts the user's role into the session, so the access policies can check it.
internal sealed class UserPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> identityOptions)
    : UserClaimsPrincipalFactory<User>(userManager, identityOptions)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);
        identity.AddClaim(new Claim(identity.RoleClaimType, user.Role.ToString()));
        return identity;
    }
}
