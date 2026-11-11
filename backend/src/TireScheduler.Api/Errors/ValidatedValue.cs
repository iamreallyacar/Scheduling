namespace TireScheduler.Api.Errors;

public static class ValidatedValue
{
    // A request field that must be present is declared nullable with [Required], so a missing
    // value is reported as a field error (AC-21) instead of silently becoming 0 or false. After
    // RequestValidationFilter has run the value is there; this turns it into the plain type.
    public static TValue Validated<TValue>(this TValue? value)
        where TValue : struct =>
        value ?? throw new InvalidOperationException("A required request field is empty; is WithRequestValidation missing on the endpoint?");
}
