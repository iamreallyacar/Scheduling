import type { RoutingRequest, RoutingResponse } from '../api/generated';

// The routing as it is being edited. Fields hold the text the Admin typed; nothing is checked
// here, the server decides what is valid. A stage has no number of its own: its sequence
// number is its place in the list, so the numbers always run 1, 2, 3.
export interface StageDraft {
  /** Identifies the stage while editing, so a dependency keeps pointing to it when stages move up. */
  key: number;
  resourceTypeId: string;
  setupMinutes: string;
  cycleMinutes: string;
  unitsPerCycle: string;
}

export interface DependencyDraft {
  key: number;
  /** Key of a stage, or null while none is chosen. */
  predecessorStageKey: number | null;
  successorStageKey: number | null;
  minLagMinutes: string;
}

export interface RoutingDraft {
  stages: readonly StageDraft[];
  dependencies: readonly DependencyDraft[];
  nextKey: number;
}

export function draftFromRouting(routing: RoutingResponse): RoutingDraft {
  // Stages arrive in sequence order, so a stage's sequence number can serve as its key.
  const stages = routing.stages.map((stage) => ({
    key: stage.sequenceNo,
    resourceTypeId: String(stage.resourceType.id),
    setupMinutes: String(stage.setupMinutes),
    cycleMinutes: String(stage.cycleMinutes),
    unitsPerCycle: String(stage.unitsPerCycle),
  }));
  const firstDependencyKey = stages.length + 1;
  const dependencies = routing.dependencies.map((dependency, position) => ({
    key: firstDependencyKey + position,
    predecessorStageKey: dependency.predecessorSequenceNo,
    successorStageKey: dependency.successorSequenceNo,
    minLagMinutes: String(dependency.minLagMinutes),
  }));
  return { stages, dependencies, nextKey: firstDependencyKey + dependencies.length };
}

/** Adds a stage at the end, depending on the stage before it with lag 0 (AC-38). */
export function addStage(draft: RoutingDraft): RoutingDraft {
  const newStage: StageDraft = { key: draft.nextKey, resourceTypeId: '', setupMinutes: '', cycleMinutes: '', unitsPerCycle: '' };
  const previousStage = draft.stages.at(-1);
  if (previousStage === undefined) {
    return { ...draft, stages: [newStage], nextKey: draft.nextKey + 1 };
  }
  const chainLink: DependencyDraft = { key: draft.nextKey + 1, predecessorStageKey: previousStage.key, successorStageKey: newStage.key, minLagMinutes: '0' };
  return { stages: [...draft.stages, newStage], dependencies: [...draft.dependencies, chainLink], nextKey: draft.nextKey + 2 };
}

/** Removes a stage and the dependencies that name it (AC-143). */
export function removeStage(draft: RoutingDraft, stageKey: number): RoutingDraft {
  return {
    ...draft,
    stages: draft.stages.filter((stage) => stage.key !== stageKey),
    dependencies: draft.dependencies.filter((dependency) => dependency.predecessorStageKey !== stageKey && dependency.successorStageKey !== stageKey),
  };
}

export function changeStage(draft: RoutingDraft, stageKey: number, change: Partial<Omit<StageDraft, 'key'>>): RoutingDraft {
  return { ...draft, stages: draft.stages.map((stage) => (stage.key === stageKey ? { ...stage, ...change } : stage)) };
}

export function addDependency(draft: RoutingDraft): RoutingDraft {
  const newDependency: DependencyDraft = { key: draft.nextKey, predecessorStageKey: null, successorStageKey: null, minLagMinutes: '0' };
  return { ...draft, dependencies: [...draft.dependencies, newDependency], nextKey: draft.nextKey + 1 };
}

export function removeDependency(draft: RoutingDraft, dependencyKey: number): RoutingDraft {
  return { ...draft, dependencies: draft.dependencies.filter((dependency) => dependency.key !== dependencyKey) };
}

export function changeDependency(draft: RoutingDraft, dependencyKey: number, change: Partial<Omit<DependencyDraft, 'key'>>): RoutingDraft {
  return { ...draft, dependencies: draft.dependencies.map((dependency) => (dependency.key === dependencyKey ? { ...dependency, ...change } : dependency)) };
}

export function sequenceNoOf(draft: RoutingDraft, stageKey: number | null): number | null {
  const position = draft.stages.findIndex((stage) => stage.key === stageKey);
  return position === -1 ? null : position + 1;
}

/** What is sent to the server. An empty field is sent as null, so the server reports it or applies its default. */
export function toRoutingRequest(draft: RoutingDraft): RoutingRequest {
  return {
    stages: draft.stages.map((stage, position) => ({
      sequenceNo: position + 1,
      resourceTypeId: numberOrNull(stage.resourceTypeId),
      setupMinutes: numberOrNull(stage.setupMinutes),
      cycleMinutes: numberOrNull(stage.cycleMinutes),
      unitsPerCycle: numberOrNull(stage.unitsPerCycle),
    })),
    dependencies: draft.dependencies.map((dependency) => ({
      predecessorSequenceNo: sequenceNoOf(draft, dependency.predecessorStageKey),
      successorSequenceNo: sequenceNoOf(draft, dependency.successorStageKey),
      minLagMinutes: numberOrNull(dependency.minLagMinutes),
    })),
  };
}

function numberOrNull(text: string): number | null {
  return text.trim() === '' ? null : Number(text);
}
