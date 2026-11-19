using TireScheduler.PlantGenerator;

namespace TireScheduler.Engine.Tests;

// AC-127 over generated plants (DEC-56), some of whose lots are placed first, so a proposal meets
// placed lots of its own order as well as other orders' placements and downtime.
public sealed class ScheduleOrderAsapPropertyTests
{
    private const string Berlin = GeneratedPlants.Berlin;

    private static readonly TimeSpan Horizon = TimeSpan.FromDays(90);

    [Theory]
    [MemberData(nameof(GeneratedPlants.Cases), MemberType = typeof(GeneratedPlants))]
    public void AC_127_the_proposed_placements_of_an_order_pass_Validate_together(int seed, DateOnly startDate)
    {
        GeneratedPlant plant = SyntheticPlant.Generate(PlantPresets.Small, seed, startDate);
        DateTimeOffset planStart = PlantTime.StartOfDay(plant.StartDate, Berlin);
        int planMinutes = (int)(PlantTime.StartOfDay(plant.EndDate, Berlin) - planStart).TotalMinutes;
        SeededRandom random = new(seed);
        ScheduleSnapshot snapshot = GeneratedPlants.WithSomeLotsPlaced(PlantSnapshot.From(plant, Berlin), random, planStart, planMinutes);
        HashSet<int> placedLotIds = [.. snapshot.Placements.Select(placement => placement.LotId)];

        int proposedLots = 0;
        foreach (int[] orderLotIds in GeneratedPlants.LotIdsByOrder(plant, snapshot))
        {
            DateTimeOffset notBefore = planStart.AddMinutes(random.Next(0, planMinutes));
            OrderProposal proposal = ScheduleOrderAsap.Propose(snapshot, new OrderProposalRequest(orderLotIds, notBefore, Horizon));

            Assert.Empty(proposal.Violations);
            if (proposal.LotWithoutSlot is not null)
            {
                Assert.Empty(proposal.Placements);
                continue;
            }

            Assert.Equal(orderLotIds.Where(lotId => !placedLotIds.Contains(lotId)).Order(), proposal.Placements.Select(placement => placement.LotId).Order());
            // Ids below zero cannot meet the placements already in the snapshot.
            ScheduleSnapshot withProposal = snapshot with
            {
                Placements =
                [
                    .. snapshot.Placements,
                    .. proposal.Placements.Select((placement, index) => new SnapshotPlacement(-(index + 1), placement.LotId, placement.ResourceId, placement.PlannedStart, placement.PlannedEnd)),
                ],
            };
            PlacementContext context = new(withProposal);
            foreach (ProposedPlacement placement in proposal.Placements)
            {
                PlacementCheck check = context.Validate(new PlacementRequest(placement.LotId, placement.ResourceId, placement.PlannedStart));
                Assert.True(check.IsAccepted, $"Seed {seed}: the proposed placement of lot {placement.LotId} on resource {placement.ResourceId} at {placement.PlannedStart:O} breaks {string.Join(", ", check.Violations.Select(violation => violation.Code))}.");
                Assert.Equal(placement.PlannedEnd, check.PlannedEnd);
                Assert.True(placement.PlannedStart >= notBefore);
            }

            proposedLots += proposal.Placements.Count;
        }

        Assert.True(proposedLots > 0, "No order got a proposal, so the property was not checked.");
    }
}
