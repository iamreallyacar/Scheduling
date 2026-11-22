using System.Globalization;
using TireScheduler.PlantGenerator;

namespace TireScheduler.PlantLoader;

// Reads the options and the sign-in settings before any request is sent (AC-160).
public static class LoaderCommandLine
{
    public const string EmailVariable = "PlantLoader__Email";
    public const string PasswordVariable = "PlantLoader__Password";

    public const string Usage =
        "Usage: TireScheduler.PlantLoader --preset <small|plant|stress> [--seed <number>] [--start <yyyy-MM-dd>] [--api <url>] [--schedule]\n" +
        $"Signs in with the environment variables {EmailVariable} and {PasswordVariable}.";

    // The one option that takes no value.
    private const string ScheduleFlag = "--schedule";

    private static readonly Uri DefaultApiUrl = new("http://localhost:5080");

    public static LoaderSettings Parse(IReadOnlyList<string> arguments, Func<string, string?> readEnvironmentVariable, DateOnly todayUtc)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        (Dictionary<string, string> values, bool placeSchedule) = ReadOptions(arguments);

        string presetName = values.GetValueOrDefault("--preset")
            ?? throw new LoaderUsageException($"--preset is required.\n{Usage}");
        if (!PlantPresets.ByName.TryGetValue(presetName, out PlantParameters? parameters))
        {
            throw new LoaderUsageException($"--preset '{presetName}' is unknown; use one of {string.Join(", ", PlantPresets.ByName.Keys)}.");
        }

        return new LoaderSettings(
            presetName,
            parameters,
            values.TryGetValue("--seed", out string? seedText) ? ParseSeed(seedText) : 1,
            values.TryGetValue("--start", out string? startText) ? ParseStartDate(startText) : MondayOfWeek(todayUtc),
            values.TryGetValue("--api", out string? apiText) ? ParseApiUrl(apiText) : DefaultApiUrl,
            RequiredVariable(readEnvironmentVariable, EmailVariable),
            RequiredVariable(readEnvironmentVariable, PasswordVariable),
            placeSchedule);
    }

    private static (Dictionary<string, string> Values, bool PlaceSchedule) ReadOptions(IReadOnlyList<string> arguments)
    {
        string[] knownOptions = ["--preset", "--seed", "--start", "--api"];
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        bool placeSchedule = false;
        int position = 0;
        while (position < arguments.Count)
        {
            string option = arguments[position];
            if (option == ScheduleFlag)
            {
                placeSchedule = true;
                position++;
                continue;
            }

            if (!knownOptions.Contains(option, StringComparer.Ordinal))
            {
                throw new LoaderUsageException($"{option} is not an option.\n{Usage}");
            }

            if (position + 1 >= arguments.Count)
            {
                throw new LoaderUsageException($"{option} needs a value.\n{Usage}");
            }

            values[option] = arguments[position + 1];
            position += 2;
        }

        return (values, placeSchedule);
    }

    private static int ParseSeed(string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int seed)
            ? seed
            : throw new LoaderUsageException($"--seed '{text}' is not a whole number.");

    private static DateOnly ParseStartDate(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : throw new LoaderUsageException($"--start '{text}' is not a date in the form yyyy-MM-dd.");

    private static Uri ParseApiUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out Uri? url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : throw new LoaderUsageException($"--api '{text}' is not an http or https address.");

    // A32: the week starts on Monday.
    private static DateOnly MondayOfWeek(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static string RequiredVariable(Func<string, string?> readEnvironmentVariable, string name) =>
        readEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new LoaderUsageException($"The environment variable {name} is not set.");
}
