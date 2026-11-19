import { useState } from 'react';
import { conflictLinksOn } from './conflictLinks';
import { useConflictedPlacements, useTimeline } from './scheduleQueries';
import { lastShownDay, type TimelineZoom } from './timelineRange';

/**
 * The timeline's data, filters and selected bar. The page holds it, because the conflict links it
 * builds are also shown by the placement form and the conflict list.
 */
export function useTimelineView(firstDay: string, zoom: TimelineZoom) {
  const lastDay = lastShownDay(firstDay, zoom);
  const timeline = useTimeline({ from: firstDay, to: lastDay });
  const conflictedPlacements = useConflictedPlacements();
  const [selectedPlacementId, setSelectedPlacementId] = useState<number | null>(null);
  const [resourceTypeId, setResourceTypeId] = useState<number | null>(null);
  const [onlyConflicted, setOnlyConflicted] = useState(false);

  // The item may be hidden by a filter, so a link clears them before it opens the bar.
  const renderConflictLink = conflictLinksOn(timeline.data, (placementId) => {
    setResourceTypeId(null);
    setOnlyConflicted(false);
    setSelectedPlacementId(placementId);
  });

  return {
    firstDay,
    lastDay,
    zoom,
    timeline,
    conflictedPlacements,
    selectedPlacementId,
    setSelectedPlacementId,
    resourceTypeId,
    setResourceTypeId,
    onlyConflicted,
    setOnlyConflicted,
    renderConflictLink,
  };
}

export type TimelineView = ReturnType<typeof useTimelineView>;
