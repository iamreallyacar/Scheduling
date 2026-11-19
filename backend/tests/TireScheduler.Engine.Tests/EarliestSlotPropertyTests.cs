using TireScheduler.PlantGenerator;

namespace TireScheduler.Engine.Tests;

// AC-115 and AC-116 over generated plants (DEC-32). Some lots are placed first at random starts
// that pass Validate, so the search meets placements and dependencies as well as downtime.
// 2027-03-22 is the week of the spring-forward change in Europe/Berlin.
public sealed class EarliestSlotPropertyTests
{
    private const string Berlin = "Europe/Berlin";
    private const int PlacementAttemptsPerLot = 5;
    private const int StartGridMinutes = 15;

    private static readonly TimeSpan Horizon = TimeSpan.FromDays(90);

    public static TheoryData<int, DateOnly> Plants() => new()
    {
        { 1, new DateOnly(2026, 11, 16) },
        { 42, new DateOnly(2027, 3, 22) },
        { 2026, new DateOnly(2026, 10, 26) },
        { -7, new DateOnly(2027, 3, 22) },
    };

    [Theory]
    [MemberData(nameof(Plants))]
    public void AC_115_AC_116_every_slot_passes_Validate_and_no_earlier_minute_on_its_resource_does(int seed, DateOnly startDate)
    {
        GeneratedPlant plant = SyntheticPlant.Generate(PlantPresets.Small, seed, startDate);
        DateTimeOffset planStart = PlantTime.StartOfDay(plant.StartDate, Berlin);
        int planMinutes = (int)(PlantTime.StartOfDay(plant.EndDate, Berlin) - planStart).TotalMinutes;
        SeededRandom random = new(seed);
        ScheduleSnapshot snapshot = WithSomeLotsPlaced(PlantSnapshot.From(plant, Berlin), random, planStart, planMinutes);
        PlacementContext context = new(snapshot);

        int slotsFound = 0;
        foreach (SnapshotLot lot in snapshot.Lots)
        {
            DateTimeOffset notBefore = planStart.AddMinutes(random.Next(0, planMinutes)).AddSeconds(random.Next(0, 60));
            EarliestSlotResult result = EarliestSlot.Find(snapshot, new EarliestSlotRequest(lot.Id, null, notBefore, Horizon));

            // A45: the search tries whole minutes from notBefore on.
            DateTimeOffset firstMinute = notBefore.Second == 0 ? notBefore : notBefore.AddSeconds(60 - notBefore.Second);
            Assert.Empty(result.Violations);
            if (result.Slot is not { } slot)
            {
                AssertNothingFitsOnTheGrid(context, snapshot, lot, firstMinute);
                continue;
            }

            slotsFound++;
            PlacementCheck check = context.Validate(new PlacementRequest(lot.Id, slot.ResourceId, slot.PlannedStart));
            Assert.Empty(check.Violations);
            Assert.Equal(slot.PlannedEnd, check.PlannedEnd);
            Assert.True(slot.PlannedStart >= firstMinute);
            for (DateTimeOffset start = firstMinute; start < slot.PlannedStart; start = start.AddMinutes(1))
            {
                Assert.False(context.Validate(new PlacementRequest(lot.Id, slot.ResourceId, start)).IsAccepted, $"Lot {lot.Id} fits on resource {slot.ResourceId} at {start:O}, before the slot at {slot.PlannedStart:O}.");
            }
        }

        Assert.True(slotsFound > 0, "The plant gave no slot at all, so the properties were not checked.");
    }

    // A missing slot is checked on a sample, every grid step of the horizon on every active
    // resource of the lot's type, because every minute would be too slow.
    private static void AssertNothingFitsOnTheGrid(PlacementContext context, ScheduleSnapshot snapshot, SnapshotLot lot, DateTimeOffset firstMinute)
    {
        DateTimeOffset searchEnd = firstMinute + Horizon;
        foreach (SnapshotResource resource in snapshot.Resources.Where(resource => resource.IsActive && resource.ResourceTypeId == lot.ResourceTypeId))
        {
            for (DateTimeOffset start = firstMinute; start < searchEnd; start = start.AddMinutes(StartGridMinutes))
            {
                Assert.False(context.Validate(new PlacementRequest(lot.Id, resource.Id, start)).IsAccepted, $"Lot {lot.Id} got no slot but fits on resource {resource.Id} at {start:O}.");
            }
        }
    }

    private static ScheduleSnapshot WithSomeLotsPlaced(ScheduleSnapshot snapshot, SeededRandom random, DateTimeOffset planStart, int planMinutes)
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
}
