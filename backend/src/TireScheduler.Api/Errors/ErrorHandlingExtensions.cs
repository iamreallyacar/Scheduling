using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Routing;

namespace TireScheduler.Api.Errors;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddSchedulerErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblems.ApplyStatusDefaults);
        services.AddExceptionHandler<ApiExceptionHandler>();
        // An unreadable body raises an exception in every environment, so ApiExceptionHandler
        // answers it; by default the framework would answer with an empty 400 outside development.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.ConfigureHttpJsonOptions(options =>
        {
            // Strict: "5" is not accepted for a number, so wrong value types are rejected (AC-22).
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        });
        return services;
    }

    // Must be the first middleware, so it also covers errors raised by the middleware after it.
    public static IApplicationBuilder UseSchedulerErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class =>
        builder.AddEndpointFilter<RequestValidationFilter<TRequest>>();
}
