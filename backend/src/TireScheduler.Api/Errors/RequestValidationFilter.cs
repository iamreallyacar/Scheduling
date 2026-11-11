using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace TireScheduler.Api.Errors;

// Checks the data annotations of a request body before the endpoint runs and reports every
// invalid field in one answer (AC-21). It looks at the body's own properties only; a request
// with nested objects needs its nested checks added here first.
internal sealed class RequestValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        TRequest request = context.Arguments.OfType<TRequest>().Single();

        List<ValidationResult> failures = [];
        if (!Validator.TryValidateObject(request, new ValidationContext(request), failures, validateAllProperties: true))
        {
            throw ApiProblemException.ValidationFailed(GroupByJsonFieldName(failures));
        }

        return await next(context).ConfigureAwait(false);
    }

    private static Dictionary<string, IReadOnlyList<string>> GroupByJsonFieldName(List<ValidationResult> failures) =>
        failures
            .SelectMany(failure => failure.MemberNames.Select(memberName => (memberName, failure.ErrorMessage)))
            .GroupBy(pair => JsonNamingPolicy.CamelCase.ConvertName(pair.memberName), pair => pair.ErrorMessage ?? "Not valid.")
            .ToDictionary(group => group.Key, IReadOnlyList<string> (group) => group.ToArray());
}
