using System.Globalization;
using TireScheduler.PlantGenerator;

namespace TireScheduler.PlantLoader;

public sealed record LoadSummary(int MachineTypes, int Machines, int Shifts, int Holidays, int Products, int Orders, int Unavailabilities);

// Writes a generated plant through the API, master data first and orders last, so every row
// passes the validation of user input (DEC-32). Ids of written rows are looked up by name.
internal sealed class PlantWriter(SchedulerApiClient api)
{
    public async Task<LoadSummary> WriteAsync(GeneratedPlant plant, CancellationToken cancellationToken)
    {
        await EnsureEmptyAsync(cancellationToken);
        Dictionary<string, int> machineTypeIds = await WriteMachineTypesAsync(plant, cancellationToken);
        Dictionary<string, int> machineIds = await WriteMachinesAsync(plant, machineTypeIds, cancellationToken);
        string plantTimeZone = await WriteCalendarAsync(plant, cancellationToken);
        await WriteUnavailabilitiesAsync(plant, machineIds, plantTimeZone, cancellationToken);
        Dictionary<string, int> productIds = await WriteProductsAsync(plant, machineTypeIds, cancellationToken);
        await WriteOrdersAsync(plant, productIds, cancellationToken);
        return new LoadSummary(
            plant.MachineTypes.Count,
            plant.Machines.Count,
            plant.Shifts.Count,
            plant.Holidays.Count,
            plant.Products.Count,
            plant.Orders.Count,
            plant.Unavailabilities.Count);
    }

    // A31: generated rows never mix with entered ones.
    private async Task EnsureEmptyAsync(CancellationToken cancellationToken)
    {
        NamedItem[] resourceTypes = await api.GetAsync<NamedItem[]>("/resource-types", cancellationToken);
        ProductResponse[] products = await api.GetAsync<ProductResponse[]>("/products", cancellationToken);
        OrderPageResponse orders = await api.GetAsync<OrderPageResponse>("/orders?pageSize=1", cancellationToken);

        List<string> existing = [];
        if (resourceTypes.Length > 0)
        {
            existing.Add($"{resourceTypes.Length} resource type(s), for example \"{resourceTypes[0].Name}\"");
        }

        if (products.Length > 0)
        {
            existing.Add($"{products.Length} product(s), for example \"{products[0].Code}\"");
        }

        if (orders.TotalCount > 0)
        {
            existing.Add($"{orders.TotalCount} order(s)");
        }

        if (existing.Count > 0)
        {
            throw new PlantNotEmptyException($"The plant is not empty, so nothing was loaded. It has {string.Join(", ", existing)}.");
        }
    }

    private async Task<Dictionary<string, int>> WriteMachineTypesAsync(GeneratedPlant plant, CancellationToken cancellationToken)
    {
        Dictionary<string, int> idByName = new(StringComparer.Ordinal);
        foreach (GeneratedMachineType machineType in plant.MachineTypes)
        {
            NamedItem created = await api.SendAsync<NamedItem>(HttpMethod.Post, "/resource-types", new ResourceTypeRequest(machineType.Name, "Machine"), cancellationToken);
            idByName[machineType.Name] = created.Id;
        }

        return idByName;
    }

    private async Task<Dictionary<string, int>> WriteMachinesAsync(GeneratedPlant plant, Dictionary<string, int> machineTypeIds, CancellationToken cancellationToken)
    {
        Dictionary<string, int> idByName = new(StringComparer.Ordinal);
        foreach (GeneratedMachine machine in plant.Machines)
        {
            NamedItem created = await api.SendAsync<NamedItem>(HttpMethod.Post, "/resources", new ResourceRequest(machine.Name, machineTypeIds[machine.MachineTypeName]), cancellationToken);
            idByName[machine.Name] = created.Id;
        }

        return idByName;
    }

    // Returns the plant time zone that every calendar answer carries.
    private async Task<string> WriteCalendarAsync(GeneratedPlant plant, CancellationToken cancellationToken)
    {
        CalendarResponse[] calendars = await api.GetAsync<CalendarResponse[]>("/calendars", cancellationToken);
        CalendarResponse plantDefault = calendars.SingleOrDefault(calendar => calendar.IsPlantDefault)
            ?? throw new LoaderException("The API has no plant default calendar.");
        CalendarShiftsRequest shifts = new([.. plant.Shifts.Select(shift => new CalendarShiftRequest(shift.Weekday, ShiftTime(shift.StartMinute), ShiftTime(shift.EndMinute)))]);
        await api.SendAsync<CalendarResponse>(HttpMethod.Put, $"/calendars/{plantDefault.Id}/shifts", shifts, cancellationToken);

        foreach (GeneratedHoliday holiday in plant.Holidays)
        {
            await api.SendAsync(HttpMethod.Post, "/holidays", new HolidayRequest(holiday.Date, holiday.Name), cancellationToken);
        }

        return plantDefault.TimeZone;
    }

    // The generated starts are plant-local (A33) and the API takes UTC.
    private async Task WriteUnavailabilitiesAsync(GeneratedPlant plant, Dictionary<string, int> machineIds, string plantTimeZone, CancellationToken cancellationToken)
    {
        foreach (GeneratedUnavailability unavailability in plant.Unavailabilities)
        {
            UnavailabilityRequest request = new(unavailability.Kind.ToString(), unavailability.StartUtc(plantTimeZone), unavailability.EndUtc(plantTimeZone), unavailability.Reason);
            await api.SendAsync(HttpMethod.Post, $"/resources/{machineIds[unavailability.MachineName]}/unavailabilities", request, cancellationToken);
        }
    }

    private async Task<Dictionary<string, int>> WriteProductsAsync(GeneratedPlant plant, Dictionary<string, int> machineTypeIds, CancellationToken cancellationToken)
    {
        Dictionary<string, int> idByCode = new(StringComparer.Ordinal);
        foreach (GeneratedProduct product in plant.Products)
        {
            ProductResponse created = await api.SendAsync<ProductResponse>(HttpMethod.Post, "/products", new ProductRequest(product.Code, product.Name, product.Spec), cancellationToken);
            RoutingRequest routing = new(
                [.. product.Stages.Select(stage => new RoutingStageRequest(stage.SequenceNo, machineTypeIds[stage.MachineTypeName], stage.SetupMinutes, stage.CycleMinutes, stage.UnitsPerCycle))],
                [.. product.Dependencies.Select(dependency => new RoutingDependencyRequest(dependency.PredecessorSequenceNo, dependency.SuccessorSequenceNo, dependency.MinLagMinutes))]);
            await api.SendAsync(HttpMethod.Put, $"/products/{created.Id}/routing", routing, cancellationToken);
            idByCode[product.Code] = created.Id;
        }

        return idByCode;
    }

    private async Task WriteOrdersAsync(GeneratedPlant plant, Dictionary<string, int> productIds, CancellationToken cancellationToken)
    {
        foreach (GeneratedOrder order in plant.Orders)
        {
            OrderRequest request = new(order.OrderNo, order.CustomerName, productIds[order.ProductCode], order.Quantity, order.DueDate, order.Priority.ToString());
            await api.SendAsync(HttpMethod.Post, "/orders", request, cancellationToken);
        }
    }

    // Shift times are plant-local "HH:mm", with "24:00" for the midnight that ends the day (API.md section "Conventions").
    private static string ShiftTime(int minuteOfDay) =>
        string.Create(CultureInfo.InvariantCulture, $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}");
}
