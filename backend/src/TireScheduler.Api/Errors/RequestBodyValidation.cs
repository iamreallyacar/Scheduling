using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;

namespace TireScheduler.Api.Errors;

// Checks the body of an endpoint before the endpoint runs and reports every invalid field,
// nested ones included, in one answer (AC-21). Added to every endpoint of a group, so no
// endpoint needs a step of its own (AC-163, DEC-35).
internal static class RequestBodyValidation
{
    public static void AddTo(EndpointBuilder endpoint) =>
        endpoint.FilterFactories.Add((factoryContext, next) => CheckBodyBefore(next, endpoint, factoryContext.MethodInfo));

    private static EndpointFilterDelegate CheckBodyBefore(EndpointFilterDelegate next, EndpointBuilder endpoint, MethodInfo handler)
    {
        // Filters are built after the framework has recorded, as accepts metadata, the body type
        // it inferred from the handler. A group convention runs before that, so it cannot read it.
        Type? bodyType = endpoint.Metadata.OfType<IAcceptsMetadata>().Select(accepts => accepts.RequestType).LastOrDefault(type => type is not null);
        if (bodyType is null)
        {
            return next;
        }

        endpoint.Metadata.Add(new ValidatedRequestBodyMetadata { BodyType = bodyType });
        int bodyPosition = Array.FindIndex(handler.GetParameters(), parameter => parameter.ParameterType == bodyType);
        if (bodyPosition < 0)
        {
            throw new InvalidOperationException($"The endpoint handler {handler.Name} reads a {bodyType.Name} body but has no parameter of that type.");
        }

        return invocation =>
        {
            // A missing body never gets here: the framework rejects it as malformed (AC-22).
            if (invocation.Arguments[bodyPosition] is { } body)
            {
                Dictionary<string, IReadOnlyList<string>> fieldErrors = RequestBodyValidator.FindFieldErrors(body);
                if (fieldErrors.Count > 0)
                {
                    throw ApiProblemException.ValidationFailed(fieldErrors);
                }
            }

            return next(invocation);
        };
    }
}
