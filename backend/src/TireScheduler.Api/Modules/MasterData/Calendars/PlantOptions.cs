using Microsoft.Extensions.Options;

namespace TireScheduler.Api.Modules.MasterData.Calendars;

// Settings under "Plant" (GUIDE.md section "Configuration"). The time zone is set once and not
// editable in the app (A18), so there is no default: a wrong zone would shift every calendar.
internal sealed class PlantOptions
{
    public const string SectionName = "Plant";

    public string? TimeZone { get; init; }
}

// AC-41: a missing value or one that is not an IANA zone id stops startup, naming the value.
internal sealed class PlantOptionsValidator : IValidateOptions<PlantOptions>
{
    private const string SettingName = $"{PlantOptions.SectionName}:{nameof(PlantOptions.TimeZone)}";

    public ValidateOptionsResult Validate(string? name, PlantOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TimeZone))
        {
            return ValidateOptionsResult.Fail(
                $"Missing setting {SettingName} (environment variable {PlantOptions.SectionName}__{nameof(PlantOptions.TimeZone)}), "
                + "the IANA time zone id of the plant, for example Europe/Berlin.");
        }

        // .NET also finds Windows zone ids; only an IANA id may be configured (ARCHITECTURE.md section "Time rules").
        bool isIanaZone = TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out TimeZoneInfo? zone) && zone.HasIanaId;
        return isIanaZone
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Setting {SettingName} has the value '{options.TimeZone}', which is not a known IANA time zone id such as Europe/Berlin.");
    }
}
