namespace TireScheduler.Api.Security;

// Settings under "Security" (README.md, section Configuration).
public sealed class CookieSecurityOptions
{
    public const string SectionName = "Security";

    // True marks the session and antiforgery cookies Secure, so browsers send them over HTTPS
    // only (AC-1). False is for local development over plain HTTP and nothing else.
    public bool CookiesRequireHttps { get; init; } = true;

    public CookieSecurePolicy SecurePolicy => CookiesRequireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
}
