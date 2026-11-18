namespace TireScheduler.Api.Database;

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Scheduler";

    public string ConnectionString { get; set; } = string.Empty;
}
