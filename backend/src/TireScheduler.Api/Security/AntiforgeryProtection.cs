using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Security;

// The session is a cookie, which a browser also sends with requests made by other sites. Every
// state-changing request must therefore carry a token that only our own pages can read (DEC-21).
public static class AntiforgeryProtection
{
    public const string HeaderName = "X-XSRF-TOKEN";

    public static IServiceCollection AddAntiforgeryProtection(this IServiceCollection services)
    {
        services.AddOptions<CookieSecurityOptions>().BindConfiguration(CookieSecurityOptions.SectionName);
        services.AddAntiforgery();
        services.AddOptions<AntiforgeryOptions>().Configure<IOptions<CookieSecurityOptions>>((antiforgery, cookieSecurity) =>
        {
            antiforgery.HeaderName = HeaderName;
            antiforgery.Cookie.Name = "tire-scheduler-antiforgery";
            antiforgery.Cookie.SecurePolicy = cookieSecurity.Value.SecurePolicy;
        });
        return services;
    }

    // Place after UseAuthorization, so a request without a session is answered 401 first (AC-6).
    public static IApplicationBuilder UseAntiforgeryValidation(this IApplicationBuilder app) =>
        app.Use(async (HttpContext context, RequestDelegate next) =>
        {
            if (context.GetEndpoint() is not null
                && ChangesState(context.Request.Method)
                && !await context.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(context).ConfigureAwait(false))
            {
                throw ApiProblemException.Rejected(
                    StatusCodes.Status400BadRequest,
                    ProblemCodes.AuthAntiforgery,
                    "The request is missing its security token. Reload the page and try again.");
            }

            await next(context).ConfigureAwait(false);
        });

    // Reads are left alone, so they also work where HTTPS is not required of them, such as a
    // health probe from inside the hosting platform.
    private static bool ChangesState(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));
}
