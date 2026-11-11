namespace TireScheduler.Api.Errors;

// The stable `code` values of problem details (ARCHITECTURE.md section 7). Clients branch on
// these, so a value never changes once released.
public static class ProblemCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string RequestMalformed = "request.malformed";
    public const string NotFound = "not_found";
    public const string ConflictDuplicate = "conflict.duplicate";
    public const string ConflictInUse = "conflict.in_use";
    public const string AuthRequired = "auth.required";
    public const string AuthForbidden = "auth.forbidden";
    public const string AuthInvalidCredentials = "auth.invalid_credentials";
    public const string AuthLockedOut = "auth.locked_out";
    public const string AuthAntiforgery = "auth.antiforgery";
    public const string ServerError = "server.error";
}
