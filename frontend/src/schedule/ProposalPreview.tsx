import type { OrderProposalResponse, TimelineResponse } from '../api/generated';
import { usePlantTimeZone } from '../calendar/plantTimeZone';
import { QueryResult } from '../forms/QueryResult';
import { useTimeline } from './scheduleQueries';
import { TimelineChart, type ProposedTimelineBar } from './TimelineChart';
import { lastShownDay, plantToday, type TimelineZoom } from './timelineRange';
import { useNow } from './useNow';

const PREVIEW_ZOOM: TimelineZoom = 'two-weeks';
// The preview marks no conflicts, so no stored bar is drawn as conflicted.
const NO_CONFLICT_MARKERS: ReadonlySet<number> = new Set();

interface ProposalPreviewProps {
  proposal: OrderProposalResponse;
  orderNo: string;
}

/** The machines a proposal uses, with what is stored on them and the proposed bars drawn apart, over the two weeks from the first lot's start (wireframes.md section 4, A46). */
export function ProposalPreview({ proposal, orderNo }: ProposalPreviewProps) {
  const timeZone = usePlantTimeZone();
  const nowMs = useNow();
  const firstStartMs = Math.min(...proposal.lots.map((lot) => Date.parse(lot.plannedStart)));
  const firstDay = plantToday(timeZone, new Date(firstStartMs));
  const timeline = useTimeline({ from: firstDay, to: lastShownDay(firstDay, PREVIEW_ZOOM) });

  return (
    <QueryResult query={timeline}>
      {(loadedTimeline) => {
        const usedResourceIds = new Set(proposal.lots.map((lot) => lot.resource.id));
        const rangeStartMs = Date.parse(loadedTimeline.start);
        const rangeEndMs = Date.parse(loadedTimeline.end);
        const proposedBars: ProposedTimelineBar[] = proposal.lots
          .filter((lot) => lot.isProposed && Date.parse(lot.plannedEnd) > rangeStartMs && Date.parse(lot.plannedStart) < rangeEndMs)
          .map((lot) => ({ lotId: lot.lotId, resourceId: lot.resource.id, label: `${orderNo}/L${String(lot.lotNo)}`, plannedStart: lot.plannedStart, plannedEnd: lot.plannedEnd }));
        return (
          <TimelineChart
            timeline={machinesUsed(loadedTimeline, usedResourceIds)}
            firstDay={firstDay}
            zoom={PREVIEW_ZOOM}
            nowMs={nowMs}
            conflictedPlacementIds={NO_CONFLICT_MARKERS}
            selectedPlacementId={null}
            onSelect={null}
            proposedBars={proposedBars}
          />
        );
      }}
    </QueryResult>
  );
}

function machinesUsed(timeline: TimelineResponse, resourceIds: ReadonlySet<number>): TimelineResponse {
  return {
    ...timeline,
    resourceTypes: timeline.resourceTypes
      .map((resourceType) => ({ ...resourceType, resources: resourceType.resources.filter((resource) => resourceIds.has(resource.id)) }))
      .filter((resourceType) => resourceType.resources.length > 0),
  };
}
