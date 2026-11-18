using System.Globalization;

namespace TireScheduler.Engine;

// Lookups over one snapshot, so each rule reads only the rows it needs. Everything is built in the
// constructor and never changes, so one index serves any number of checks (PlacementContext).
internal sealed class SnapshotIndex
{
    private readonly ScheduleSnapshot snapshot;
    private readonly Dictionary<int, SnapshotLot> lotsById;
    private readonly Dictionary<int, SnapshotResource> resourcesById;
    private readonly Dictionary<int, WorkingTime> workingTimeByCalendar;
    private readonly Dictionary<int, SnapshotPlacement> placementsByLot;
    private readonly ILookup<int, SnapshotPlacement> placementsByResource;
    private readonly ILookup<int, SnapshotUnavailability> unavailabilitiesByResource;
    private readonly ILookup<int, SnapshotLot> lotsByOperation;
    private readonly ILookup<int, StageDependency> dependenciesBySuccessor;
    private readonly ILookup<int, StageDependency> dependenciesByPredecessor;

    public SnapshotIndex(ScheduleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        this.snapshot = snapshot;
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(snapshot.PlantTimeZoneId);
        lotsById = snapshot.Lots.ToDictionary(lot => lot.Id);
        resourcesById = snapshot.Resources.ToDictionary(resource => resource.Id);
        workingTimeByCalendar = snapshot.Calendars.ToDictionary(calendar => calendar.Id, calendar => new WorkingTime(calendar, snapshot.Holidays, zone));

        // A version holds at most one placement per lot (unique (version, lot)).
        placementsByLot = snapshot.Placements.ToDictionary(placement => placement.LotId);
        placementsByResource = snapshot.Placements.ToLookup(placement => placement.ResourceId);
        unavailabilitiesByResource = snapshot.Unavailabilities.ToLookup(unavailability => unavailability.ResourceId);
        lotsByOperation = snapshot.Lots.ToLookup(lot => lot.OperationId);
        dependenciesBySuccessor = snapshot.Dependencies.ToLookup(dependency => dependency.SuccessorOperationId);
        dependenciesByPredecessor = snapshot.Dependencies.ToLookup(dependency => dependency.PredecessorOperationId);
    }

    public IReadOnlyList<SnapshotPlacement> Placements => snapshot.Placements;

    public SnapshotLot Lot(int lotId) =>
        lotsById.TryGetValue(lotId, out SnapshotLot? lot) ? lot : throw NotInSnapshot("Lot", lotId, nameof(lotId));

    public SnapshotResource Resource(int resourceId) =>
        resourcesById.TryGetValue(resourceId, out SnapshotResource? resource) ? resource : throw NotInSnapshot("Resource", resourceId, nameof(resourceId));

    public WorkingTime WorkingTimeOf(SnapshotResource resource) =>
        workingTimeByCalendar.TryGetValue(resource.CalendarId, out WorkingTime? workingTime)
            ? workingTime
            : throw NotInSnapshot("Calendar", resource.CalendarId, nameof(resource));

    public IEnumerable<SnapshotPlacement> PlacementsOn(int resourceId) => placementsByResource[resourceId];

    public IEnumerable<SnapshotUnavailability> UnavailabilitiesOf(int resourceId) => unavailabilitiesByResource[resourceId];

    // The placed lots of the operations this operation waits for, each with the lag of its link.
    public IEnumerable<(SnapshotPlacement Placement, int MinLagMinutes)> PlacedPredecessors(int operationId) =>
        PlacedLotsOf(dependenciesBySuccessor[operationId], dependency => dependency.PredecessorOperationId);

    public IEnumerable<(SnapshotPlacement Placement, int MinLagMinutes)> PlacedSuccessors(int operationId) =>
        PlacedLotsOf(dependenciesByPredecessor[operationId], dependency => dependency.SuccessorOperationId);

    private IEnumerable<(SnapshotPlacement Placement, int MinLagMinutes)> PlacedLotsOf(IEnumerable<StageDependency> dependencies, Func<StageDependency, int> linkedOperation) =>
        from dependency in dependencies
        from lot in lotsByOperation[linkedOperation(dependency)]
        where placementsByLot.ContainsKey(lot.Id)
        select (placementsByLot[lot.Id], dependency.MinLagMinutes);

    private static ArgumentException NotInSnapshot(string kind, int id, string parameterName) =>
        new(string.Create(CultureInfo.InvariantCulture, $"{kind} {id} is not in the snapshot."), parameterName);
}
