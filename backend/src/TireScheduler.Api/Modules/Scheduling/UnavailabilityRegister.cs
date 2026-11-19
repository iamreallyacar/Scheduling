using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.MasterData;

namespace TireScheduler.Api.Modules.Scheduling;

// F-MNT-1: record, change and delete the unavailabilities of a resource. Recording one locks the
// resource row, so the resource cannot be deleted at the same moment (DEC-33, AC-20). A new or
// changed unavailability is always saved, also over placed lots (DEC-18).
internal sealed class UnavailabilityRegister(
    SchedulingDbContext context,
    IMasterDataReader masterData,
    IMasterDataLocks masterDataLocks,
    RequestTransaction requestTransaction)
{
    // Latest start first (AC-168).
    public async Task<IReadOnlyList<UnavailabilityResponse>> ListAsync(int resourceId, CancellationToken cancellationToken)
    {
        if (!await masterData.ResourceExistsAsync(resourceId, cancellationToken))
        {
            throw MachineNotFound();
        }

        return await context.Unavailabilities
            .Where(unavailability => unavailability.ResourceId == resourceId)
            .OrderByDescending(unavailability => unavailability.StartsAt)
            .ThenByDescending(unavailability => unavailability.Id)
            .Select(UnavailabilityResponses.FromRow)
            .ToListAsync(cancellationToken);
    }

    public async Task<UnavailabilityResponse> CreateAsync(int resourceId, UnavailabilityRequest request, CancellationToken cancellationToken)
    {
        CheckedValues values = Check(request);
        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        if (!await masterDataLocks.LockResourceAsync(resourceId, cancellationToken))
        {
            throw MachineNotFound();
        }

        Unavailability unavailability = new() { ResourceId = resourceId };
        values.ApplyTo(unavailability);
        context.Unavailabilities.Add(unavailability);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UnavailabilityResponses.ToResponse(unavailability);
    }

    // No resource lock: the resource keeps this unavailability, so it cannot be deleted meanwhile (AC-20).
    public async Task<UnavailabilityResponse> UpdateAsync(int resourceId, int unavailabilityId, UnavailabilityRequest request, CancellationToken cancellationToken)
    {
        CheckedValues values = Check(request);
        Unavailability unavailability = await FindAsync(resourceId, unavailabilityId, cancellationToken);
        values.ApplyTo(unavailability);
        await context.SaveChangesAsync(cancellationToken);
        return UnavailabilityResponses.ToResponse(unavailability);
    }

    public async Task DeleteAsync(int resourceId, int unavailabilityId, CancellationToken cancellationToken)
    {
        Unavailability unavailability = await FindAsync(resourceId, unavailabilityId, cancellationToken);
        context.Unavailabilities.Remove(unavailability);
        await context.SaveChangesAsync(cancellationToken);
    }

    // An unavailability is found only under the resource it belongs to (AC-169).
    private async Task<Unavailability> FindAsync(int resourceId, int unavailabilityId, CancellationToken cancellationToken) =>
        await context.Unavailabilities.FirstOrDefaultAsync(
            unavailability => unavailability.Id == unavailabilityId && unavailability.ResourceId == resourceId,
            cancellationToken)
        ?? throw ApiProblemException.NotFound("This unavailability");

    // AC-167: the checks that span fields or that the data annotations cannot express.
    private static CheckedValues Check(UnavailabilityRequest request)
    {
        UnavailabilityKind kind = request.Kind.Validated();
        DateTimeOffset start = request.Start.Validated();
        Dictionary<string, IReadOnlyList<string>> fieldErrors = [];
        if (!IsWholeMinute(start))
        {
            fieldErrors["start"] = ["The start must be a whole minute."];
        }

        if (request.End is { } end)
        {
            if (!IsWholeMinute(end))
            {
                fieldErrors["end"] = ["The end must be a whole minute."];
            }
            else if (end <= start)
            {
                fieldErrors["end"] = ["The end must be after the start."];
            }
        }
        else if (kind == UnavailabilityKind.Planned)
        {
            fieldErrors["end"] = ["Planned maintenance needs an end; only a breakdown can have no known end."];
        }

        if (fieldErrors.Count > 0)
        {
            throw ApiProblemException.ValidationFailed(fieldErrors);
        }

        string? reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        return new CheckedValues { Kind = kind, Start = start.ToUniversalTime(), End = request.End?.ToUniversalTime(), Reason = reason };
    }

    // Times are stored in whole minutes (A2).
    private static bool IsWholeMinute(DateTimeOffset time) => time.Ticks % TimeSpan.TicksPerMinute == 0;

    private static ApiProblemException MachineNotFound() => ApiProblemException.NotFound("This machine");

    private sealed record CheckedValues
    {
        public required UnavailabilityKind Kind { get; init; }

        public required DateTimeOffset Start { get; init; }

        public required DateTimeOffset? End { get; init; }

        public required string? Reason { get; init; }

        public void ApplyTo(Unavailability unavailability)
        {
            unavailability.Kind = Kind;
            unavailability.StartsAt = Start;
            unavailability.EndsAt = End;
            unavailability.Reason = Reason;
        }
    }
}
