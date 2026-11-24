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

        client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
        try
        {
            GeneratedPlant plant = GeneratePlant(settings);
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Loading preset {settings.PresetName}, seed {settings.Seed}, start {settings.StartDate:yyyy-MM-dd}, into {client.BaseAddress}."));
            SchedulerApiClient api = new(client);
            await api.SignInAsync(settings.Email, settings.Password, cancellationToken);
            WrittenPlant written = await new PlantWriter(api).WriteAsync(plant, cancellationToken);
            LoadSummary summary = written.Summary;
            await output.WriteLineAsync(
                $"Loaded {summary.MachineTypes} machine types, {summary.Machines} machines, {summary.Shifts} shifts, {summary.Holidays} holidays, " +
                $"{summary.Products} products with routings, {summary.Orders} orders and {summary.Unavailabilities} unavailabilities.");
            if (settings.PlaceSchedule)
            {
                ScheduleSummary schedule = await new ScheduleWriter(api).PlaceAsync(plant, written, cancellationToken);
                await output.WriteLineAsync($"Placed {schedule.PlacedLots} lots of {schedule.PlacedOrders} orders in the live schedule.");
                if (schedule.OrderNosWithoutSlot.Count > 0)
                {
                    await output.WriteLineAsync(
                        $"{schedule.OrderNosWithoutSlot.Count} orders have no free slot within the search horizon: {string.Join(", ", schedule.OrderNosWithoutSlot.Order(StringComparer.Ordinal))}.");
                }
            }

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

    // The checks of AC-156 fail with an ArgumentException naming the parameter.
    private static GeneratedPlant GeneratePlant(LoaderSettings settings)
    {
        try
        {
            return SyntheticPlant.Generate(settings.Parameters, settings.Seed, settings.StartDate);
        }
        // Exact type: the checks throw a plain ArgumentException, while a subclass such as ArgumentNullException is a generator bug.
        catch (ArgumentException failure) when (failure.GetType() == typeof(ArgumentException))
        {
            throw new LoaderException($"The plant parameters are not valid: {failure.Message}", failure);
        }
    }
}
