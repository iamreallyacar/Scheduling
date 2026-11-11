using Microsoft.AspNetCore.Mvc;

namespace TireScheduler.Api.Errors;

// Builds the problem details bodies of ARCHITECTURE.md section 7.
public static class ApiProblems
{
    public const string CodeField = "code";
    public const string FieldErrorsField = "errors";
    public const string CorrelationIdField = "correlationId";

    public static ProblemDetails Create(int status, string code, string detail)
    {
        ProblemDetails problem = new() { Status = status, Detail = detail };
        problem.Extensions[CodeField] = code;
        return problem;
    }

    // For responses that carry only a status code (no session, wrong role, unknown route,
    // unreadable body): the code and the text shown to the user follow from the status.
    public static ProblemDetails ForStatus(int status)
    {
        (string code, string detail) = status switch
        {
            StatusCodes.Status400BadRequest => (ProblemCodes.RequestMalformed, "The request could not be read. Check that it is valid JSON with values of the right type."),
            StatusCodes.Status401Unauthorized => (ProblemCodes.AuthRequired, "Sign in to continue."),
            StatusCodes.Status403Forbidden => (ProblemCodes.AuthForbidden, "Your role does not allow this action."),
            StatusCodes.Status404NotFound => (ProblemCodes.NotFound, "The requested item does not exist."),
            _ => ($"http.{status}", "The request was rejected."),
        };
        return Create(status, code, detail);
    }

    // Nothing about the failure goes into the body; the correlation id links it to the log entry.
    public static ProblemDetails Unexpected(string correlationId)
    {
        ProblemDetails problem = Create(
            StatusCodes.Status500InternalServerError,
            ProblemCodes.ServerError,
            "Something went wrong on the server. Give the correlation id to support.");
        problem.Extensions[CorrelationIdField] = correlationId;
        return problem;
    }

    internal static void ApplyStatusDefaults(ProblemDetailsContext context)
    {
        ProblemDetails problem = context.ProblemDetails;
        if (problem.Extensions.ContainsKey(CodeField))
        {
            return;
        }

        ProblemDetails defaults = ForStatus(problem.Status ?? context.HttpContext.Response.StatusCode);
        problem.Extensions[CodeField] = defaults.Extensions[CodeField];
        problem.Detail ??= defaults.Detail;
    }
}
