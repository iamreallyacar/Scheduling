using TireScheduler.PlantGenerator;

namespace TireScheduler.Engine.Tests;

// The generated plants of the property tests (DEC-56). 2027-03-22 is the week of the
// spring-forward change in Europe/Berlin.
internal static class GeneratedPlants
{
    public const string Berlin = "Europe/Berlin";

    private const int PlacementAttemptsPerLot = 5;
    private const int StartGridMinutes = 15;

    public static TheoryData<int, DateOnly> Cases() => new()
    {
        { 1, new DateOnly(2026, 11, 16) },
        { 42, new DateOnly(2027, 3, 22) },
        { 2026, new DateOnly(2026, 10, 26) },
        { -7, new DateOnly(2027, 3, 22) },
    };

    // Some lots are placed first at random starts that pass Validate, so the searches meet
    // placements and dependencies as well as downtime.
    public static ScheduleSnapshot WithSomeLotsPlaced(ScheduleSnapshot snapshot, SeededRandom random, DateTimeOffset planStart, int planMinutes)
    {
        int placementId = 1;
        foreach (SnapshotLot lot in snapshot.Lots)
        {
            SnapshotResource[] resourcesOfType = [.. snapshot.Resources.Where(resource => resource.ResourceTypeId == lot.ResourceTypeId)];
            for (int attempt = 0; attempt < PlacementAttemptsPerLot; attempt++)
            {
                SnapshotResource resource = resourcesOfType[random.Next(0, resourcesOfType.Length)];
                DateTimeOffset start = planStart.AddMinutes(StartGridMinutes * random.Next(0, planMinutes / StartGridMinutes));
                PlacementCheck check = PlacementRules.Validate(snapshot, new PlacementRequest(lot.Id, resource.Id, start));
                if (check.IsAccepted && check.PlannedEnd is { } plannedEnd)
                {
                    snapshot = snapshot.WithPlacement(placementId++, lot.Id, resource.Id, start, plannedEnd);
                    break;
                }
            }
        }

        return snapshot;
    }

    // PlantSnapshot numbers the lots in the order of the generated orders and their stages
    // (AC-193), so the lots of each order follow one another.
    public static IEnumerable<int[]> LotIdsByOrder(GeneratedPlant plant, ScheduleSnapshot snapshot)
    {
        Dictionary<string, int> stageCountByProduct = plant.Products.ToDictionary(product => product.Code, product => product.Stages.Count);
        int firstLotIndex = 0;
        foreach (GeneratedOrder order in plant.Orders)
        {
            int stageCount = stageCountByProduct[order.ProductCode];
            yield return [.. snapshot.Lots.Skip(firstLotIndex).Take(stageCount).Select(lot => lot.Id)];
            firstLotIndex += stageCount;
        }
    }
}
