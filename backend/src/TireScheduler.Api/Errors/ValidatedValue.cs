namespace TireScheduler.Api.Errors;

public static class ValidatedValue
{
    private const string NotValidatedMessage =
        "A required request field is empty, so the body was not validated; is the endpoint mapped outside the /api/v1 group (DEC-35)?";

    // A request field that must be present is declared nullable with [Required], so a missing
    // value is reported as a field error (AC-21) instead of silently becoming 0 or false. After
    // RequestBodyValidation has run the value is there; this turns it into the plain type.
    public static TValue Validated<TValue>(this TValue? value)
        where TValue : struct =>
        value ?? throw new InvalidOperationException(NotValidatedMessage);

    public static TValue Validated<TValue>(this TValue? value)
        where TValue : class =>
        value ?? throw new InvalidOperationException(NotValidatedMessage);
}
