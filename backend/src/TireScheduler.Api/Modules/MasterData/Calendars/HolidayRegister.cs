using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.MasterData.Calendars;

// F-MD-5: the plant holiday list, shared by every calendar. A change recomputes the planned ends
// in its transaction (DEC-44).
internal sealed class HolidayRegister(MasterDataDbContext context, RequestTransaction requestTransaction, WorkingTimeChange workingTimeChange)
{
    private static readonly Dictionary<string, string> FieldByUniqueIndex = new() { [MasterDataDbContext.HolidayDateIndex] = "date" };

    public async Task<IReadOnlyList<HolidayResponse>> ListAsync(CancellationToken cancellationToken) =>
        await context.Holidays
            .OrderBy(holiday => holiday.Date)
            .Select(holiday => new HolidayResponse { Id = holiday.Id, Date = holiday.Date, Name = holiday.Name })
            .ToListAsync(cancellationToken);

    public async Task<HolidayResponse> CreateAsync(HolidayRequest request, CancellationToken cancellationToken)
    {
        Holiday holiday = new() { Date = request.Date.Validated(), Name = request.Name.Trim() };
        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        await workingTimeChange.LockEveryResourceAsync(cancellationToken);
        context.Holidays.Add(holiday);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken);
        await workingTimeChange.RecomputePlannedEndsAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new HolidayResponse { Id = holiday.Id, Date = holiday.Date, Name = holiday.Name };
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using OpenTransaction transaction = await requestTransaction.BeginAsync(context, cancellationToken);
        Holiday holiday = await context.Holidays.FirstOrDefaultAsync(holiday => holiday.Id == id, cancellationToken)
            ?? throw ApiProblemException.NotFound("This holiday");
        await workingTimeChange.LockEveryResourceAsync(cancellationToken);
        context.Holidays.Remove(holiday);
        await context.SaveChangesAsync(cancellationToken);
        await workingTimeChange.RecomputePlannedEndsAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
