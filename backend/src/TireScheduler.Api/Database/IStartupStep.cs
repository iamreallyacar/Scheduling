namespace TireScheduler.Api.Database;

// Work that must finish before the API takes requests, such as applying migrations. The steps
// run once, in the order they were registered, and a failing step stops startup.
public interface IStartupStep
{
    Task RunAsync(CancellationToken cancellationToken);
}
