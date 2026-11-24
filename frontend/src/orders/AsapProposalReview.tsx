import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import type { OrderProposalResponse, OrderResponse } from '../api/generated';
import { commitOrderAsapProposal } from '../api/generated/endpoints.gen';
import { hasProblemCode, problemOf, type ApiProblem } from '../api/problem';
import { SCHEDULE_VERSION } from '../api/scheduleVersion';
import { formatPlantTime } from '../calendar/plantClock';
import { usePlantTimeZone } from '../calendar/plantTimeZone';
import { START_GRID_STEP_SECONDS, TextField } from '../forms/Fields';
import { readPlantTimes } from '../forms/plantTimeFields';
import { ProblemSummary } from '../forms/ProblemSummary';
import { ProposalPreview } from '../schedule/ProposalPreview';
import { useAsapProposal, type AsapProposalSearch } from '../schedule/scheduleQueries';
import { ViolationList } from '../schedule/ViolationList';

const PROBLEM_CODE_PROPOSAL_STALE = 'proposal.stale';

interface AsapProposalReviewProps {
  order: OrderResponse;
  onClose: () => void;
}

// F-SCH-16 (wireframes.md section 4). The server proposes, checks every rule and says whether the
// order would be late (DEC-60); the review shows its answer and saves only on commit, sending
// the proposal unchanged.
export function AsapProposalReview({ order, onClose }: AsapProposalReviewProps) {
  const timeZone = usePlantTimeZone();
  const [notBefore, setNotBefore] = useState('');
  // The text the shown proposal was asked with; display state only, the server decides the proposal.
  const [askedText, setAskedText] = useState('');
  // Without notBefore the server proposes from its own now (AC-125).
  const [search, setSearch] = useState<AsapProposalSearch>({});
  const [timeProblem, setTimeProblem] = useState<ApiProblem | null>(null);
  const proposal = useAsapProposal(order.id, search);
  const commitMutation = useMutation({
    mutationFn: (committed: OrderProposalResponse) =>
      commitOrderAsapProposal(SCHEDULE_VERSION, order.id, {
        notBefore: committed.notBefore,
        placements: committed.lots
          .filter((lot) => lot.isProposed)
          .map((lot) => ({ lotId: lot.lotId, resourceId: lot.resource.id, plannedStart: lot.plannedStart })),
      }),
    onSuccess: onClose,
  });
  const proposalProblem = timeProblem ?? (proposal.isError ? problemOf(proposal.error) : null);
  const title = `Schedule order ${order.orderNo} as soon as possible`;

  const recalculate = () => {
    commitMutation.reset();
    const reading = readPlantTimes({ notBefore }, timeZone);
    setTimeProblem(reading.problem);
    if (reading.problem !== null) {
      return;
    }
    setAskedText(notBefore);
    const typedSearch: AsapProposalSearch = reading.utc.notBefore === null ? {} : { notBefore: reading.utc.notBefore };
    // The same search from now is asked again, because now has moved on.
    if (typedSearch.notBefore === search.notBefore) {
      void proposal.refetch();
    } else {
      setSearch(typedSearch);
    }
  };

  return (
    <section className="panel" aria-label={title}>
      <h2>{title}</h2>
      <p>
        Product {order.product.code} {order.product.name}, quantity {order.quantity}, due {order.dueDate}. Times in {timeZone}.
      </p>
      <TextField label="Not before" name="notBefore" type="datetime-local" step={START_GRID_STEP_SECONDS} value={notBefore} onChange={setNotBefore} problem={proposalProblem} />
      <p>Empty means now.</p>
      <div className="form-actions">
        <button type="button" disabled={proposal.isFetching} onClick={recalculate}>
          Recalculate
        </button>
      </div>
      {proposal.isPending && <p className="page-message">Loading…</p>}
      <ProblemSummary problem={proposalProblem} />
      {proposalProblem !== null && <ViolationList violations={proposalProblem.violations} />}
      {proposal.isSuccess && (
        <ProposalContent
          proposal={proposal.data}
          order={order}
          // An answer being asked again is not yet the one on screen.
          commitDisabled={commitMutation.isPending || proposal.isFetching || notBefore !== askedText}
          isTimeChanged={notBefore !== askedText}
          onCommit={() => {
            commitMutation.mutate(proposal.data);
          }}
        />
      )}
      {hasProblemCode(commitMutation.error, PROBLEM_CODE_PROPOSAL_STALE) ? (
        <p className="problem" role="alert">
          The schedule changed since this proposal was made. Nothing was saved.
        </p>
      ) : (
        <>
          <ProblemSummary problem={problemOf(commitMutation.error)} />
          <ViolationList violations={problemOf(commitMutation.error)?.violations ?? []} />
        </>
      )}
      <div className="form-actions">
        <button type="button" onClick={onClose}>
          Cancel
        </button>
      </div>
    </section>
  );
}

interface ProposalContentProps {
  proposal: OrderProposalResponse;
  order: OrderResponse;
  commitDisabled: boolean;
  isTimeChanged: boolean;
  onCommit: () => void;
}

function ProposalContent({ proposal, order, commitDisabled, isTimeChanged, onCommit }: ProposalContentProps) {
  const timeZone = usePlantTimeZone();
  const proposedCount = proposal.lots.filter((lot) => lot.isProposed).length;
  if (proposedCount === 0) {
    return <p className="page-message">Every lot of this order is already placed.</p>;
  }

  return (
    <>
      <table aria-label="Proposal">
        <thead>
          <tr>
            <th scope="col">Seq</th>
            <th scope="col">Stage</th>
            <th scope="col">Lot</th>
            <th scope="col">Machine</th>
            <th scope="col">Start</th>
            <th scope="col">End</th>
            <th scope="col">Note</th>
          </tr>
        </thead>
        <tbody>
          {proposal.lots.map((lot) => (
            <tr key={lot.lotId}>
              <td>{lot.sequenceNo}</td>
              <td>{lot.resourceType.name}</td>
              <td>{lot.lotNo}</td>
              <td>{lot.resource.name}</td>
              <td>{formatPlantTime(lot.plannedStart, timeZone)}</td>
              <td>{formatPlantTime(lot.plannedEnd, timeZone)}</td>
              <td>{lot.isProposed ? 'Proposed' : 'Already placed'}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <h3>Preview (machines used by this order)</h3>
      <ProposalPreview proposal={proposal} orderNo={order.orderNo} />
      <p>{`Order would finish ${formatPlantTime(proposal.finishesAt, timeZone)}, due ${order.dueDate}: ${proposal.wouldBeLate ? 'late' : 'on time'}`}</p>
      {isTimeChanged && <p className="page-message">Recalculate to use the new time</p>}
      <p>Nothing is saved until you commit. Existing placements are never moved.</p>
      <div className="form-actions">
        <button type="button" disabled={commitDisabled} onClick={onCommit}>
          {`Commit ${String(proposedCount)} placements`}
        </button>
      </div>
    </>
  );
}
