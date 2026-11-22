namespace TireScheduler.PlantLoader;

// The loader's own copies of the request and answer shapes it uses. They are kept small and
// checked against the real API by the loader's API tests (DEC-32).
internal sealed record SignInRequest(string Email, string Password);

internal sealed record AntiforgeryTokenResponse(string HeaderName, string Token);

internal sealed record NamedItem(int Id, string Name);

internal sealed record ResourceTypeRequest(string Name, string Kind);

internal sealed record ResourceRequest(string Name, int ResourceTypeId);

internal sealed record CalendarResponse(int Id, bool IsPlantDefault, string TimeZone);

internal sealed record CalendarShiftsRequest(IReadOnlyList<CalendarShiftRequest> Shifts);

internal sealed record CalendarShiftRequest(DayOfWeek Weekday, string Start, string End);

internal sealed record HolidayRequest(DateOnly Date, string Name);

internal sealed record ProductRequest(string Code, string Name, string Spec);

internal sealed record ProductResponse(int Id, string Code);

internal sealed record RoutingRequest(IReadOnlyList<RoutingStageRequest> Stages, IReadOnlyList<RoutingDependencyRequest> Dependencies);

internal sealed record RoutingStageRequest(int SequenceNo, int ResourceTypeId, int SetupMinutes, int CycleMinutes, int UnitsPerCycle);

internal sealed record RoutingDependencyRequest(int PredecessorSequenceNo, int SuccessorSequenceNo, int MinLagMinutes);

internal sealed record OrderRequest(string OrderNo, string CustomerName, int ProductId, int Quantity, DateOnly DueDate, string Priority);

internal sealed record OrderCreatedResponse(int Id);

internal sealed record OrderPageResponse(int TotalCount);

// A whole-order proposal (DEC-60); the commit sends back its start and its proposed lots.
internal sealed record OrderProposalResponse(DateTimeOffset NotBefore, IReadOnlyList<ProposalLotResponse> Lots);

internal sealed record ProposalLotResponse(int LotId, NamedItem Resource, DateTimeOffset PlannedStart, bool IsProposed);

internal sealed record ProposalCommitRequest(DateTimeOffset NotBefore, IReadOnlyList<ProposedPlacementRequest> Placements);

internal sealed record ProposedPlacementRequest(int LotId, int ResourceId, DateTimeOffset PlannedStart);

// Times in UTC; no end for a breakdown with no known end.
internal sealed record UnavailabilityRequest(string Kind, DateTimeOffset Start, DateTimeOffset? End, string Reason);

internal sealed record ProblemResponse(string? Code, string? Detail);
