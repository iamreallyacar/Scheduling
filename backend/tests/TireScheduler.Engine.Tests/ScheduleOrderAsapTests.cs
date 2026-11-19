using static TireScheduler.Engine.Tests.TestPlant;

namespace TireScheduler.Engine.Tests;

// F-SCH-16 at engine level (acceptance-criteria.md section 5, "ScheduleOrderAsap"). A lot's
// operation is numbered as the lot plus 200 unless the test gives one operation several lots.
public sealed class ScheduleOrderAsapTests
{
    private const int ExtrusionType = 3;
    private const int InspectionType = 4;
    private const int Extruder1 = 21;
    private const int Inspector1 = 22;
    private const int BuildingLotMinutes = 120;
    private const int CuringLotMinutes = 240;
    private const int RestBeforeCuringMinutes = 120;

    private static readonly TimeSpan Horizon = TimeSpan.FromDays(90);

    [Fact]
    public void AC_128_a_linear_chain_gets_each_earliest_slot_in_dependency_order_with_the_lag_before_curing()
    {
        ScheduleSnapshot plant = Empty();
        ScheduleSnapshot snapshot = (plant with
        {
            Resources =
            [
                .. plant.Resources,
                new SnapshotResource(Extruder1, "Extruder 1", ExtrusionType, PlantCalendarId, IsActive: true),
                new SnapshotResource(Inspector1, "Inspector 1", InspectionType, PlantCalendarId, IsActive: true),
            ],
        })
            .WithLot(101, 60, ExtrusionType, operationId: 301, sequenceNo: 1)
            .WithLot(102, BuildingLotMinutes, BuildingType, operationId: 302, sequenceNo: 2)
            .WithLot(103, CuringLotMinutes, CuringType, operationId: 303, sequenceNo: 3)
            .WithLot(104, 30, InspectionType, operationId: 304, sequenceNo: 4)
            .WithDependency(301, 302)
            .WithDependency(302, 303, RestBeforeCuringMinutes)
            .WithDependency(303, 304);

        OrderProposal proposal = Propose(snapshot, [101, 102, 103, 104], Mon(8));

        Assert.Equal(
            [
                new ProposedPlacement(101, Extruder1, Mon(8), Mon(9)),
                new ProposedPlacement(102, Builder1, Mon(9), Mon(11)),
                new ProposedPlacement(103, Press1, Mon(13), Mon(17)),
                new ProposedPlacement(104, Inspector1, Mon(17), Mon(17, 30)),
            ],
            proposal.Placements);
        Assert.Null(proposal.LotWithoutSlot);
        Assert.Empty(proposal.Violations);
    }

    [Fact]
    public void AC_129_a_stage_waits_for_both_stages_it_depends_on_and_independent_stages_go_by_sequence_number()
    {
        // Stage 2 has the lower lot and operation ids, so only the sequence number puts stage 1 first.
        ScheduleSnapshot snapshot = Empty()
            .WithLot(112, BuildingLotMinutes, BuildingType, operationId: 312, sequenceNo: 1)
            .WithLot(111, BuildingLotMinutes, BuildingType, operationId: 311, sequenceNo: 2)
            .WithLot(113, CuringLotMinutes, CuringType, operationId: 313, sequenceNo: 3)
            .WithDependency(312, 313)
            .WithDependency(311, 313);

        OrderProposal proposal = Propose(snapshot, [113, 111, 112], Mon(8));

        Assert.Equal(
            [
                new ProposedPlacement(112, Builder1, Mon(8), Mon(10)),
                new ProposedPlacement(111, Builder1, Mon(10), Mon(12)),
                new ProposedPlacement(113, Press1, Mon(12), Mon(16)),
            ],
            proposal.Placements);
    }

    [Fact]
    public void AC_129_dependency_order_comes_before_the_sequence_number()
    {
        // A routing may let stage 1 wait for stage 2, so stage 2 goes first.
        ScheduleSnapshot snapshot = Empty()
            .WithLot(151, BuildingLotMinutes, BuildingType, operationId: 351, sequenceNo: 1)
            .WithLot(152, BuildingLotMinutes, BuildingType, operationId: 352, sequenceNo: 2)
            .WithDependency(352, 351);

        OrderProposal proposal = Propose(snapshot, [151, 152], Mon(8));

        Assert.Equal(
            [
                new ProposedPlacement(152, Builder1, Mon(8), Mon(10)),
                new ProposedPlacement(151, Builder1, Mon(10), Mon(12)),
            ],
            proposal.Placements);
    }

    [Fact]
    public void AC_129_lots_of_one_stage_go_by_lot_id_and_the_next_stage_waits_for_all_of_them()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(162, BuildingLotMinutes, BuildingType, operationId: 361, sequenceNo: 1)
            .WithLot(161, BuildingLotMinutes, BuildingType, operationId: 361, sequenceNo: 1)
            .WithLot(163, CuringLotMinutes, CuringType, operationId: 362, sequenceNo: 2)
            .WithDependency(361, 362);

        OrderProposal proposal = Propose(snapshot, [162, 163, 161], Mon(8));

        Assert.Equal(
            [
                new ProposedPlacement(161, Builder1, Mon(8), Mon(10)),
                new ProposedPlacement(162, Builder1, Mon(10), Mon(12)),
                new ProposedPlacement(163, Press1, Mon(12), Mon(16)),
            ],
            proposal.Placements);
    }

    [Fact]
    public void AC_130_only_unplaced_lots_are_proposed_and_the_building_lot_ends_the_lag_before_the_placed_curing_lot()
    {
        ScheduleSnapshot snapshot = WithCuringPlacedOnMondayAfternoon();

        OrderProposal proposal = Propose(snapshot, [121, 122], Mon(10));

        // The curing lot starts Mon 14:00, so the building lot ends by 12:00 with the 2-hour rest.
        Assert.Equal([new ProposedPlacement(121, Builder1, Mon(10), Mon(12))], proposal.Placements);
        Assert.Null(proposal.LotWithoutSlot);
    }

    [Fact]
    public void AC_131_no_proposal_when_a_lot_has_no_slot_and_the_lot_is_named()
    {
        ScheduleSnapshot snapshot = WithCuringPlacedOnMondayAfternoon();

        // From 10:01 the building lot ends after 12:00, too late for the placed curing lot.
        OrderProposal proposal = Propose(snapshot, [121, 122], Mon(10, 1));

        Assert.Empty(proposal.Placements);
        Assert.Equal(121, proposal.LotWithoutSlot);
        Assert.Empty(proposal.Violations);
    }

    [Fact]
    public void AC_131_a_later_lot_without_a_slot_leaves_no_proposal_for_the_earlier_ones()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(131, BuildingLotMinutes, BuildingType, operationId: 331, sequenceNo: 1)
            .WithLot(132, CuringLotMinutes, CuringType, operationId: 332, sequenceNo: 2)
            .WithDependency(331, 332)
            .WithUnavailability(1, Press1, Mon(0), end: null)
            .WithUnavailability(2, Press2, Mon(0), end: null);

        OrderProposal proposal = Propose(snapshot, [131, 132], Mon(8));

        Assert.Empty(proposal.Placements);
        Assert.Equal(132, proposal.LotWithoutSlot);
    }

    [Fact]
    public void AC_132_an_order_with_every_lot_placed_gets_an_empty_proposal()
    {
        ScheduleSnapshot snapshot = WithCuringPlacedOnMondayAfternoon()
            .WithPlacement(2, 121, Builder1, Mon(8), Mon(10));

        OrderProposal proposal = Propose(snapshot, [121, 122], Mon(8));

        Assert.Empty(proposal.Placements);
        Assert.Null(proposal.LotWithoutSlot);
        Assert.Empty(proposal.Violations);
    }

    [Fact]
    public void AC_133_a_cancelled_order_is_rejected_under_R8_even_when_every_lot_is_placed()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(141, BuildingLotMinutes, BuildingType, operationId: 341, sequenceNo: 1, orderIsCancelled: true)
            .WithPlacement(3, 141, Builder1, Mon(8), Mon(10));

        OrderProposal proposal = Propose(snapshot, [141], Mon(8));

        Assert.Empty(proposal.Placements);
        RuleViolation violation = Assert.Single(proposal.Violations);
        Assert.Equal((RuleCodes.Cancelled, new ConflictItem(ConflictKind.Lot, 141)), (violation.Code, violation.Conflict));
    }

    [Fact]
    public void AC_124_a_started_lot_to_propose_rejects_the_order_under_R8()
    {
        ScheduleSnapshot snapshot = Empty()
            .WithLot(171, BuildingLotMinutes, BuildingType, operationId: 371, sequenceNo: 1, progress: LotProgressState.InProgress);

        OrderProposal proposal = Propose(snapshot, [171], Mon(8));

        Assert.Empty(proposal.Placements);
        Assert.Null(proposal.LotWithoutSlot);
        Assert.Equal([RuleCodes.Started], proposal.Violations.Select(violation => violation.Code));
    }

    [Fact]
    public void A_lot_given_twice_is_an_error()
    {
        ScheduleSnapshot snapshot = Empty().WithLot(181, BuildingLotMinutes, BuildingType, sequenceNo: 1);

        Assert.Throws<ArgumentException>(() => Propose(snapshot, [181, 181], Mon(8)));
    }

    // Building 121 waits 2 hours before curing 122, which is placed on Press 1, Mon 14:00-18:00.
    private static ScheduleSnapshot WithCuringPlacedOnMondayAfternoon() => Empty()
        .WithLot(121, BuildingLotMinutes, BuildingType, operationId: 321, sequenceNo: 1)
        .WithLot(122, CuringLotMinutes, CuringType, operationId: 322, sequenceNo: 2)
        .WithDependency(321, 322, RestBeforeCuringMinutes)
        .WithPlacement(1, 122, Press1, Mon(14), Mon(18));

    private static OrderProposal Propose(ScheduleSnapshot snapshot, int[] lotIds, DateTimeOffset notBefore) =>
        ScheduleOrderAsap.Propose(snapshot, new OrderProposalRequest(lotIds, notBefore, Horizon));
}
