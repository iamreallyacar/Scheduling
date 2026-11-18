using Microsoft.AspNetCore.Mvc;

namespace TireScheduler.Api.Errors;

// Thrown by module code to reject a request with a specific problem details answer. It ends the
// request, so a surrounding transaction is rolled back and nothing is saved.
public sealed class ApiProblemException : Exception
{
    private ApiProblemException(
        int status,
        string code,
        string detail,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null,
        IReadOnlyDictionary<string, object>? extensions = null)
        : base(detail)
    {
        Status = status;
        Code = code;
        FieldErrors = fieldErrors;
        Extensions = extensions;
    }

    public int Status { get; }

    public string Code { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors { get; }

    // Further fields of the problem details, for example `rule`, `conflict` and `violations` of a
    // scheduling rule violation (API.md section "Error format").
    public IReadOnlyDictionary<string, object>? Extensions { get; }

    public static ApiProblemException Rejected(int status, string code, string detail) => new(status, code, detail);

    public static ApiProblemException NotFound(string itemName) =>
        new(StatusCodes.Status404NotFound, ProblemCodes.NotFound, $"{itemName} does not exist.");

    public static ApiProblemException ValidationFailed(IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors) =>
        new(StatusCodes.Status400BadRequest, ProblemCodes.ValidationFailed, "Some fields are missing or not valid.", fieldErrors);

    public static ApiProblemException ValidationFailed(string field, string message) =>
        ValidationFailed(new Dictionary<string, IReadOnlyList<string>> { [field] = [message] });

    public static ApiProblemException Duplicate(string field, string detail) =>
        new(StatusCodes.Status409Conflict, ProblemCodes.ConflictDuplicate, detail, new Dictionary<string, IReadOnlyList<string>> { [field] = [detail] });

    public static ApiProblemException InUse(string detail) =>
        new(StatusCodes.Status409Conflict, ProblemCodes.ConflictInUse, detail);

    // A scheduling rule is broken (R7): 422, never adjusted.
    public static ApiProblemException RuleViolated(string code, string detail, IReadOnlyDictionary<string, object> extensions) =>
        new(StatusCodes.Status422UnprocessableEntity, code, detail, extensions: extensions);

    public ProblemDetails ToProblemDetails()
    {
        ProblemDetails problem = ApiProblems.Create(Status, Code, Message);
        if (FieldErrors is not null)
        {
            problem.Extensions[ApiProblems.FieldErrorsField] = FieldErrors;
        }

        foreach ((string field, object value) in Extensions ?? new Dictionary<string, object>())
        {
            problem.Extensions[field] = value;
        }

        return problem;
    }
}
