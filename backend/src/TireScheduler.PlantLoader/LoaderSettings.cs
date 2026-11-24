using TireScheduler.PlantGenerator;

namespace TireScheduler.PlantLoader;

public sealed record LoaderSettings(
    string PresetName,
    PlantParameters Parameters,
    int Seed,
    DateOnly StartDate,
    Uri ApiUrl,
    string Email,
    string Password,
    // --schedule: place every generated order in the live schedule after loading (DEC-61).
    bool PlaceSchedule,
    // Seconds one request may take (AC-227).
    int RequestTimeoutSeconds = LoaderSettings.DefaultRequestTimeoutSeconds)
{
    public const int DefaultRequestTimeoutSeconds = 300;

    // HttpClient.Timeout accepts at most int.MaxValue milliseconds.
    public const int MaxRequestTimeoutSeconds = int.MaxValue / 1000;
}
