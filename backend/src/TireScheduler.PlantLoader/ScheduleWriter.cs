using System.Globalization;
using System.Net;
using TireScheduler.Engine;
using TireScheduler.PlantGenerator;

namespace TireScheduler.PlantLoader;

public sealed record ScheduleSummary(int PlacedOrders, int PlacedLots, IReadOnlyList<string> OrderNosWithoutSlot);

// Places every generated order in the live schedule through the proposal and commit endpoints,
// earliest due date first, each from the plant-local start of the horizon (DEC-61).
internal sealed class ScheduleWriter(SchedulerApiClient api)
{
    private const string SlotNoneCode = "slot.none";

    public async Task<ScheduleSummary> PlaceAsync(GeneratedPlant plant, WrittenPlant written, CancellationToken cancellationToken)
    {
        // Only the engine converts plant-local time to UTC (ARCHITECTURE.md section "Time rules").
        DateTimeOffset horizonStart = PlantTime.StartOfDay(plant.StartDate, written.PlantTimeZone);
        string notBefore = Uri.EscapeDataString(horizonStart.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        int placedOrders = 0;
        int placedLots = 0;
        List<string> orderNosWithoutSlot = [];
        foreach (GeneratedOrder order in plant.Orders.OrderBy(order => order.DueDate).ThenBy(order => order.OrderNo, StringComparer.Ordinal))
        {
            string proposalPath = string.Create(CultureInfo.InvariantCulture, $"/versions/live/orders/{written.OrderIdsByNo[order.OrderNo]}/asap-proposal");
            OrderProposalResponse proposal;
            try
            {
                proposal = await api.SendAsync<OrderProposalResponse>(HttpMethod.Post, $"{proposalPath}?notBefore={notBefore}", body: null, cancellationToken);
            }
            catch (ApiRejectedException rejection) when (rejection.Status == (int)HttpStatusCode.UnprocessableEntity && rejection.Code == SlotNoneCode)
            {
                // AC-204: the plant leaves this order no slot, which the summary reports.
                orderNosWithoutSlot.Add(order.OrderNo);
                continue;
            }

            ProposedPlacementRequest[] placements = [.. proposal.Lots
                .Where(lot => lot.IsProposed)
                .Select(lot => new ProposedPlacementRequest(lot.LotId, lot.Resource.Id, lot.PlannedStart))];
            await api.SendAsync(HttpMethod.Post, $"{proposalPath}/commit", new ProposalCommitRequest(proposal.NotBefore, placements), cancellationToken);
            placedOrders++;
            placedLots += placements.Length;
        }

        return new ScheduleSummary(placedOrders, placedLots, orderNosWithoutSlot);
    }
}
