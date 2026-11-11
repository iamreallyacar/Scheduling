using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TireScheduler.Api.Errors;

// Turns every exception that leaves an endpoint into problem details (ARCHITECTURE.md section 7).
internal sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ApiProblemException rejection => rejection.ToProblemDetails(),
            // Thrown by the framework when the body or a parameter cannot be read.
            BadHttpRequestException badRequest => ApiProblems.ForStatus(badRequest.StatusCode),
            _ => LogAndDescribeUnexpected(httpContext, exception),
        };

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await TypedResults.Problem(problem).ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }

    private ProblemDetails LogAndDescribeUnexpected(HttpContext httpContext, Exception exception)
    {
        string correlationId = httpContext.TraceIdentifier;
        LogUnexpectedError(logger, exception, correlationId, httpContext.Request.Method, httpContext.Request.Path);
        return ApiProblems.Unexpected(correlationId);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error, correlation id {CorrelationId}, on {Method} {Path}")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception, string correlationId, string method, string path);
}
