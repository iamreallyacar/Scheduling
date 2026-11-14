using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;

namespace TireScheduler.Api.Errors;

// Checks the data annotations of a request body and of every object nested in it. A nested
// field is named by its path as the client sent it, for example `stages[0].cycleMinutes`
// (ARCHITECTURE.md section 7).
internal static class RequestBodyValidator
{
    public static Dictionary<string, IReadOnlyList<string>> FindFieldErrors(object body)
    {
        List<(string FieldPath, string Message)> failures = [];
        CollectFailures(body, pathPrefix: string.Empty, failures);
        return failures
            .GroupBy(failure => failure.FieldPath, failure => failure.Message)
            .ToDictionary(group => group.Key, IReadOnlyList<string> (group) => group.ToArray());
    }

    private static void CollectFailures(object target, string pathPrefix, List<(string FieldPath, string Message)> failures)
    {
        List<ValidationResult> ownFailures = [];
        Validator.TryValidateObject(target, new ValidationContext(target), ownFailures, validateAllProperties: true);
        failures.AddRange(ownFailures.SelectMany(failure =>
            failure.MemberNames.Select(memberName => (pathPrefix + JsonFieldName(memberName), failure.ErrorMessage ?? "Not valid."))));

        foreach (PropertyInfo property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetValue(target) is not { } value)
            {
                continue;
            }

            string fieldPath = pathPrefix + JsonFieldName(property.Name);
            if (value is IEnumerable items and not string)
            {
                CollectItemFailures(items, fieldPath, failures);
            }
            else if (IsNestedObject(value))
            {
                CollectFailures(value, $"{fieldPath}.", failures);
            }
        }
    }

    private static void CollectItemFailures(IEnumerable items, string listPath, List<(string FieldPath, string Message)> failures)
    {
        int position = 0;
        foreach (object? item in items)
        {
            string itemPath = $"{listPath}[{position}]";
            if (item is null)
            {
                failures.Add((itemPath, "This item is empty."));
            }
            else if (IsNestedObject(item))
            {
                CollectFailures(item, $"{itemPath}.", failures);
            }

            position++;
        }
    }

    // Numbers, booleans, enums and text carry no annotations of their own.
    private static bool IsNestedObject(object value) => value is not string && !value.GetType().IsValueType;

    private static string JsonFieldName(string propertyName) => JsonNamingPolicy.CamelCase.ConvertName(propertyName);
}
