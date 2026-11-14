import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { replaceRouting } from '../api/endpoints';
import type { ResourceTypeResponse, RoutingResponse } from '../api/generated';
import { problemOf } from '../api/problem';
import { NumberField, SelectField, type SelectOption } from '../forms/Fields';
import { ProblemSummary } from '../forms/ProblemSummary';
import {
  addDependency,
  addStage,
  changeDependency,
  changeStage,
  draftFromRouting,
  removeDependency,
  removeStage,
  toRoutingRequest,
  type RoutingDraft,
} from './routingDraft';
import { routingQueryKey } from './routingQueries';

interface RoutingEditorProps {
  productId: number;
  routing: RoutingResponse;
  resourceTypes: readonly ResourceTypeResponse[];
}

const NOTHING_CHOSEN = '';

// Stages and dependencies are edited together and saved as a whole (AC-34). Field names are
// the paths the API uses in `errors`, so each message lands next to its field.
export function RoutingEditor({ productId, routing, resourceTypes }: RoutingEditorProps) {
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState(() => draftFromRouting(routing));
  const saveMutation = useMutation({
    mutationFn: () => replaceRouting(productId, toRoutingRequest(draft)),
    onSuccess: (savedRouting) => {
      queryClient.setQueryData(routingQueryKey(productId), savedRouting);
      // Shows what the server saved, including the defaults it filled in.
      setDraft(draftFromRouting(savedRouting));
    },
  });
  const problem = problemOf(saveMutation.error);

  const edit = (changedDraft: RoutingDraft) => {
    // The answer to the last save no longer describes what is on screen.
    saveMutation.reset();
    setDraft(changedDraft);
  };

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    saveMutation.mutate();
  };

  const typeOptions: SelectOption[] = [
    { value: NOTHING_CHOSEN, label: 'Choose a type' },
    ...resourceTypes.map((type) => ({ value: String(type.id), label: type.name })),
  ];
  const stageOptions: SelectOption[] = [
    { value: NOTHING_CHOSEN, label: 'Choose a stage' },
    ...draft.stages.map((stage, position) => ({ value: String(stage.key), label: `Stage ${String(position + 1)}` })),
  ];
  const stageKeyOf = (chosenValue: string) => (chosenValue === NOTHING_CHOSEN ? null : Number(chosenValue));

  return (
    <form onSubmit={submit} noValidate aria-label="Routing">
      <ProblemSummary problem={problem} />
      {saveMutation.isSuccess && <p role="status">Routing saved.</p>}

      <h2>Stages</h2>
      {draft.stages.length === 0 && <p className="page-message">No stages yet.</p>}
      {draft.stages.map((stage, position) => {
        const stageName = `Stage ${String(position + 1)}`;
        const fieldPath = `stages[${String(position)}]`;
        return (
          <fieldset key={stage.key} className="routing-item">
            <legend>{stageName}</legend>
            <SelectField
              label="Machine type"
              name={`${fieldPath}.resourceTypeId`}
              value={stage.resourceTypeId}
              onChange={(resourceTypeId) => {
                edit(changeStage(draft, stage.key, { resourceTypeId }));
              }}
              problem={problem}
              options={typeOptions}
            />
            <NumberField
              label="Setup minutes (optional)"
              name={`${fieldPath}.setupMinutes`}
              min={0}
              value={stage.setupMinutes}
              onChange={(setupMinutes) => {
                edit(changeStage(draft, stage.key, { setupMinutes }));
              }}
              problem={problem}
            />
            <NumberField
              label="Cycle minutes"
              name={`${fieldPath}.cycleMinutes`}
              min={1}
              value={stage.cycleMinutes}
              onChange={(cycleMinutes) => {
                edit(changeStage(draft, stage.key, { cycleMinutes }));
              }}
              problem={problem}
            />
            <NumberField
              label="Units per cycle (optional)"
              name={`${fieldPath}.unitsPerCycle`}
              min={1}
              value={stage.unitsPerCycle}
              onChange={(unitsPerCycle) => {
                edit(changeStage(draft, stage.key, { unitsPerCycle }));
              }}
              problem={problem}
            />
            <button
              type="button"
              aria-label={`Remove ${stageName}`}
              onClick={() => {
                edit(removeStage(draft, stage.key));
              }}
            >
              Remove
            </button>
          </fieldset>
        );
      })}
      <div className="section-actions">
        <button
          type="button"
          onClick={() => {
            edit(addStage(draft));
          }}
        >
          Add stage
        </button>
      </div>

      <h2>Dependencies</h2>
      <p className="page-message">The successor stage may start only the minimum lag after the predecessor stage ends.</p>
      {draft.dependencies.map((dependency, position) => {
        const dependencyName = `Dependency ${String(position + 1)}`;
        const fieldPath = `dependencies[${String(position)}]`;
        return (
          <fieldset key={dependency.key} className="routing-item">
            <legend>{dependencyName}</legend>
            <SelectField
              label="Predecessor stage"
              name={`${fieldPath}.predecessorSequenceNo`}
              value={dependency.predecessorStageKey === null ? NOTHING_CHOSEN : String(dependency.predecessorStageKey)}
              onChange={(chosenValue) => {
                edit(changeDependency(draft, dependency.key, { predecessorStageKey: stageKeyOf(chosenValue) }));
              }}
              problem={problem}
              options={stageOptions}
            />
            <SelectField
              label="Successor stage"
              name={`${fieldPath}.successorSequenceNo`}
              value={dependency.successorStageKey === null ? NOTHING_CHOSEN : String(dependency.successorStageKey)}
              onChange={(chosenValue) => {
                edit(changeDependency(draft, dependency.key, { successorStageKey: stageKeyOf(chosenValue) }));
              }}
              problem={problem}
              options={stageOptions}
            />
            <NumberField
              label="Minimum lag (minutes)"
              name={`${fieldPath}.minLagMinutes`}
              min={0}
              value={dependency.minLagMinutes}
              onChange={(minLagMinutes) => {
                edit(changeDependency(draft, dependency.key, { minLagMinutes }));
              }}
              problem={problem}
            />
            <button
              type="button"
              aria-label={`Remove ${dependencyName}`}
              onClick={() => {
                edit(removeDependency(draft, dependency.key));
              }}
            >
              Remove
            </button>
          </fieldset>
        );
      })}
      <div className="section-actions">
        <button
          type="button"
          disabled={draft.stages.length < 2}
          onClick={() => {
            edit(addDependency(draft));
          }}
        >
          Add dependency
        </button>
      </div>

      <div className="form-actions">
        <button type="submit" disabled={saveMutation.isPending}>
          Save routing
        </button>
      </div>
    </form>
  );
}
