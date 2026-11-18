using TireScheduler.Api.Errors;
using TireScheduler.Api.Modules.Orders;

namespace TireScheduler.Api.Queries.Orders;

// The query parameters of the order list as sent (API.md section "Conventions"). Values of the
// wrong type never get here: the framework rejects them as request.malformed (AC-149).
internal sealed record OrderListParameters
{
    public required int? Page { get; init; }

    public required int? PageSize { get; init; }

    public required string? Search { get; init; }

    public required IReadOnlyList<string> Statuses { get; init; }

    public required bool? Late { get; init; }

    public required string? Customer { get; init; }

    public required DateOnly? DueFrom { get; init; }

    public required DateOnly? DueTo { get; init; }

    public required IReadOnlyList<string> Priorities { get; init; }

    public required string? Sort { get; init; }

    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;
    private const string DescendingPrefix = "-";

    // The names of the list item fields (OrderSummaryResponse) as they are sent.
    private static readonly Dictionary<string, OrderSortField> SortFieldsByName = new(StringComparer.Ordinal)
    {
        ["orderNo"] = OrderSortField.OrderNo,
        ["customerName"] = OrderSortField.CustomerName,
        ["priority"] = OrderSortField.Priority,
        ["dueDate"] = OrderSortField.DueDate,
        ["createdAt"] = OrderSortField.CreatedAt,
    };

    // Every field error at once (AC-21), or the request ready for the reader.
    public OrderListRequest ToRequest()
    {
        Dictionary<string, IReadOnlyList<string>> fieldErrors = [];
        int page = Page ?? 1;
        int pageSize = PageSize ?? DefaultPageSize;
        if (page < 1)
        {
            fieldErrors["page"] = ["The page must be 1 or more."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            fieldErrors["pageSize"] = [$"The page size must be between 1 and {MaxPageSize}."];
        }

        HashSet<OrderStatus> statuses = ParseNames<OrderStatus>(Statuses, "status", fieldErrors);
        HashSet<OrderPriority> priorities = ParseNames<OrderPriority>(Priorities, "priority", fieldErrors);
        if (DueFrom > DueTo)
        {
            fieldErrors["dueTo"] = ["The end of the due date range must not be before its start."];
        }

        OrderSort? sort = ParseSort(Sort);
        if (sort is null)
        {
            fieldErrors["sort"] = [$"Sort by one of {string.Join(", ", SortFieldsByName.Keys)}, with {DescendingPrefix} in front for descending."];
        }

        if (fieldErrors.Count > 0 || sort is null)
        {
            throw ApiProblemException.ValidationFailed(fieldErrors);
        }

        return new OrderListRequest
        {
            Criteria = new OrderListCriteria
            {
                SearchText = Search,
                CustomerText = Customer,
                DueFrom = DueFrom,
                DueTo = DueTo,
                Priorities = priorities,
                Sort = sort,
            },
            Statuses = statuses,
            Late = Late,
            Page = page,
            PageSize = pageSize,
        };
    }

    // Null when the value names no sort field.
    private static OrderSort? ParseSort(string? sort)
    {
        if (sort is null)
        {
            return OrderSort.Default;
        }

        bool descending = sort.StartsWith(DescendingPrefix, StringComparison.Ordinal);
        string fieldName = descending ? sort[DescendingPrefix.Length..] : sort;
        return SortFieldsByName.TryGetValue(fieldName, out OrderSortField field) ? new OrderSort { Field = field, Descending = descending } : null;
    }

    // Names only, exactly as the API writes them: Enum.TryParse would also take numbers.
    private static HashSet<TEnum> ParseNames<TEnum>(IReadOnlyList<string> names, string field, Dictionary<string, IReadOnlyList<string>> fieldErrors)
        where TEnum : struct, Enum
    {
        string[] knownNames = Enum.GetNames<TEnum>();
        if (!names.All(name => knownNames.Contains(name, StringComparer.Ordinal)))
        {
            fieldErrors[field] = [$"Each {field} must be one of {string.Join(", ", knownNames)}."];
            return [];
        }

        return names.Select(Enum.Parse<TEnum>).ToHashSet();
    }
}
