using TireScheduler.Api.Modules.Scheduling;

namespace TireScheduler.Api.Queries.Machines;

// The resource display status table of domain-model.md section 6, first matching row wins.
internal static class ResourceStatusRules
{
    public static ResourceStatus Derive(IReadOnlyCollection<UnavailabilityKind> kindsCoveringNow, bool isRunningALot)
    {
        if (kindsCoveringNow.Contains(UnavailabilityKind.Breakdown))
        {
            return ResourceStatus.Down;
        }

        if (kindsCoveringNow.Contains(UnavailabilityKind.Planned))
        {
            return ResourceStatus.Maintenance;
        }

        return isRunningALot ? ResourceStatus.Running : ResourceStatus.Idle;
    }
}
