using System.Globalization;
using TireScheduler.PlantGenerator;

namespace TireScheduler.PlantLoader;

// One run of the loader: generate, sign in, write, report. Returns the process exit code.
public static class LoaderRun
{
    public static async Task<int> RunAsync(LoaderSettings settings, HttpClient client, TextWriter output, TextWriter errors, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(errors);

        GeneratedPlant plant = SyntheticPlant.Generate(settings.Parameters, settings.Seed, settings.StartDate);
        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Loading preset {settings.PresetName}, seed {settings.Seed}, start {settings.StartDate:yyyy-MM-dd}, into {client.BaseAddress}."));
        try
        {
            SchedulerApiClient api = new(client);
            await api.SignInAsync(settings.Email, settings.Password, cancellationToken);
            LoadSummary summary = await new PlantWriter(api).WriteAsync(plant, cancellationToken);
            await output.WriteLineAsync(
                $"Loaded {summary.MachineTypes} machine types, {summary.Machines} machines, {summary.Shifts} shifts, {summary.Holidays} holidays, " +
                $"{summary.Products} products with routings, {summary.Orders} orders and {summary.Unavailabilities} unavailabilities.");
            return 0;
        }
        catch (LoaderException failure)
        {
            await errors.WriteLineAsync($"Loading stopped: {failure.Message}");
            return 1;
        }
        catch (HttpRequestException failure)
        {
            await errors.WriteLineAsync($"Loading stopped: the API at {client.BaseAddress} cannot be reached: {failure.Message}");
            return 1;
        }
    }
}
