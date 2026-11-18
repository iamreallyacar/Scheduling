using TireScheduler.PlantGenerator;

namespace TireScheduler.PlantLoader;

public sealed record LoaderSettings(
    string PresetName,
    PlantParameters Parameters,
    int Seed,
    DateOnly StartDate,
    Uri ApiUrl,
    string Email,
    string Password);
