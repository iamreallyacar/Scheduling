namespace TireScheduler.Api.Errors;

// Checks a request body before the endpoint runs and reports every invalid field, nested
// ones included, in one answer (AC-21).
internal sealed class RequestValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        TRequest request = context.Arguments.OfType<TRequest>().Single();

        Dictionary<string, IReadOnlyList<string>> fieldErrors = RequestBodyValidator.FindFieldErrors(request);
        if (fieldErrors.Count > 0)
        {
            throw ApiProblemException.ValidationFailed(fieldErrors);
        }

        return await next(context).ConfigureAwait(false);
    }
}
