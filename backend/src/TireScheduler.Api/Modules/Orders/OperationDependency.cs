namespace TireScheduler.Api.Modules.Orders;

// The successor operation's lots may start only MinLagMinutes after every lot of the
// predecessor operation ends (A8, A11). Copied from the routing (A1).
internal sealed class OperationDependency
{
    public int OrderId { get; set; }

    public int PredecessorOperationId { get; set; }

    public Operation? PredecessorOperation { get; set; }

    public int SuccessorOperationId { get; set; }

    public Operation? SuccessorOperation { get; set; }

    public int MinLagMinutes { get; set; }
}
