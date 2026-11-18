import type { RoutingResponse } from '../api/generated';

/** The routing for reading only. */
export function RoutingTables({ routing }: { routing: RoutingResponse }) {
  if (routing.stages.length === 0) {
    return <p className="page-message">This product has no routing yet.</p>;
  }
  return (
    <>
      <h2 id="routing-stages-heading">Stages</h2>
      <table aria-labelledby="routing-stages-heading">
        <thead>
          <tr>
            <th scope="col">Stage</th>
            <th scope="col">Machine type</th>
            <th scope="col">Setup minutes</th>
            <th scope="col">Cycle minutes</th>
            <th scope="col">Units per cycle</th>
          </tr>
        </thead>
        <tbody>
          {routing.stages.map((stage) => (
            <tr key={stage.sequenceNo}>
              <th scope="row">Stage {stage.sequenceNo}</th>
              <td>{stage.resourceType.name}</td>
              <td>{stage.setupMinutes}</td>
              <td>{stage.cycleMinutes}</td>
              <td>{stage.unitsPerCycle}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <h2 id="routing-dependencies-heading">Dependencies</h2>
      {routing.dependencies.length === 0 ? (
        <p className="page-message">No dependencies: every stage may start at any time.</p>
      ) : (
        <table aria-labelledby="routing-dependencies-heading">
          <thead>
            <tr>
              <th scope="col">Predecessor stage</th>
              <th scope="col">Successor stage</th>
              <th scope="col">Minimum lag (minutes)</th>
            </tr>
          </thead>
          <tbody>
            {routing.dependencies.map((dependency) => (
              <tr key={`${String(dependency.predecessorSequenceNo)}-${String(dependency.successorSequenceNo)}`}>
                <td>Stage {dependency.predecessorSequenceNo}</td>
                <td>Stage {dependency.successorSequenceNo}</td>
                <td>{dependency.minLagMinutes}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
