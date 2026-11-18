namespace TireScheduler.PlantLoader;

// A failure the loader reports to the user and stops on, with a non-zero exit code.
public class LoaderException(string message) : Exception(message);

public sealed class LoaderUsageException(string message) : LoaderException(message);

// AC-158.
public sealed class PlantNotEmptyException(string message) : LoaderException(message);

// AC-159: the API answered a request with an error.
// The request is its method and path, for example "POST /api/v1/products".
public sealed class ApiRejectedException(string request, int status, string? code, string? detail)
    : LoaderException($"{request} was rejected with {status} {code ?? "(no code)"}: {detail ?? "(no detail)"}");
