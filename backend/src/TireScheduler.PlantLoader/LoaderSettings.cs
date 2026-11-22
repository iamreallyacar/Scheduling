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
    bool PlaceSchedule);
