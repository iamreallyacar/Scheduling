namespace TireScheduler.Api.Modules.Scheduling;

// Settings under "Scheduling" (GUIDE.md section "Configuration"). The engine reads no
// configuration, so the API passes the horizon to each search (DEC-55).
internal sealed class SchedulingOptions
{
    public const string SectionName = "Scheduling";

    // How far after notBefore a slot may start (AC-123); its default is documented in GUIDE.md.
    public int SearchHorizonDays { get; init; }
}
