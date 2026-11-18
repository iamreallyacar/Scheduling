namespace TireScheduler.Api.Database;

// An ILIKE pattern that finds a text anywhere in a value, with % and _ in the text taken
// literally (A29). Pass EscapeCharacter to EF.Functions.ILike with it.
public static class ContainsPattern
{
    public const string EscapeCharacter = "\\";

    public static string For(string text) =>
        "%" + text
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal) + "%";
}
