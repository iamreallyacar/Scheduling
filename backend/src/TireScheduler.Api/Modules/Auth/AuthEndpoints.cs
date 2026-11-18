using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Modules.Auth;

internal static class AuthEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        RouteGroupBuilder auth = api.MapGroup(string.Empty).WithTags("Auth");
        auth.MapGet("/auth/antiforgery-token", GetAntiforgeryToken).WithName("GetAntiforgeryToken").AllowAnonymous();
        auth.MapPost("/auth/sign-in", SignInAsync).WithName("SignIn").AllowAnonymous();
        auth.MapPost("/auth/sign-out", SignOutAsync).WithName("SignOut").RequireAuthorization(AccessPolicies.Read);
        auth.MapGet("/me", GetCurrentUserAsync).WithName("GetCurrentUser").RequireAuthorization(AccessPolicies.Read);
    }

    // The token belongs to the current session, so the client asks again after sign-in and sign-out.
    private static Ok<AntiforgeryTokenResponse> GetAntiforgeryToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(httpContext);
        string requestToken = tokens.RequestToken ?? throw new InvalidOperationException("The antiforgery system returned no request token.");
        return TypedResults.Ok(new AntiforgeryTokenResponse { HeaderName = AntiforgeryProtection.HeaderName, Token = requestToken });
    }

    private static async Task<Ok<CurrentUserResponse>> SignInAsync(
        SignInRequest request,
        HttpContext httpContext,
        PasswordSignIn passwordSignIn,
        IUserClaimsPrincipalFactory<User> principalFactory,
        IAuthorizationService authorization)
    {
        User user = await passwordSignIn.VerifyAsync(request.Email, request.Password);
        ClaimsPrincipal principal = await principalFactory.CreateAsync(user);
        await httpContext.SignInAsync(SessionCookie.Scheme, principal);
        return TypedResults.Ok(await DescribeAsync(user, principal, authorization));
    }

    private static async Task<NoContent> SignOutAsync(HttpContext httpContext, UserManager<User> userManager)
    {
        User user = await FindSignedInUserAsync(httpContext.User, userManager);
        // A new security stamp ends every session of this user, also a copied cookie (AC-5, A21).
        IdentityResult result = await userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not end the sessions of user {user.Id}: {result}");
        }

        await httpContext.SignOutAsync(SessionCookie.Scheme);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<CurrentUserResponse>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<User> userManager,
        IAuthorizationService authorization)
    {
        User user = await FindSignedInUserAsync(principal, userManager);
        return TypedResults.Ok(await DescribeAsync(user, principal, authorization));
    }

    private static async Task<User> FindSignedInUserAsync(ClaimsPrincipal principal, UserManager<User> userManager) =>
        await userManager.GetUserAsync(principal)
        ?? throw new InvalidOperationException("The session passed validation but its user does not exist.");

    private static async Task<CurrentUserResponse> DescribeAsync(User user, ClaimsPrincipal principal, IAuthorizationService authorization)
    {
        string email = user.Email ?? throw new InvalidOperationException($"User {user.Id} has no email.");
        bool canPlan = (await authorization.AuthorizeAsync(principal, AccessPolicies.Plan)).Succeeded;
        bool canAdminister = (await authorization.AuthorizeAsync(principal, AccessPolicies.Administer)).Succeeded;
        return new CurrentUserResponse
        {
            Name = user.Name,
            Email = email,
            Role = user.Role,
            CanPlan = canPlan,
            CanAdminister = canAdminister,
        };
    }
}
