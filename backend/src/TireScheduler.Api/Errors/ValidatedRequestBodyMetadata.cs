namespace TireScheduler.Api.Errors;

// Marks an endpoint whose body RequestBodyValidation checks, so a test can find endpoints
// that read a body without checking it (AC-163).
internal sealed record ValidatedRequestBodyMetadata
{
    public required Type BodyType { get; init; }
}
