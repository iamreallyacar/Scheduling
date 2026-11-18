namespace TireScheduler.PlantLoader;

// Loads a generated plant into a running API (GUIDE.md section "Load a generated plant").
internal static class LoaderProgram
{
    public static async Task<int> Main(string[] arguments)
    {
        LoaderSettings settings;
        try
        {
            DateOnly todayUtc = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
            settings = LoaderCommandLine.Parse(arguments, Environment.GetEnvironmentVariable, todayUtc);
        }
        catch (LoaderUsageException failure)
        {
            await Console.Error.WriteLineAsync(failure.Message);
            return 2;
        }

        using SocketsHttpHandler handler = new() { UseCookies = true, AllowAutoRedirect = false };
        using HttpClient client = new(handler) { BaseAddress = settings.ApiUrl };
        return await LoaderRun.RunAsync(settings, client, Console.Out, Console.Error, CancellationToken.None);
    }
}
