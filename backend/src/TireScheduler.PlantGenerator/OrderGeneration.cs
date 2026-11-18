using System.Globalization;

namespace TireScheduler.PlantGenerator;

internal static class OrderGeneration
{
    // Fictional customers.
    private static readonly string[] Customers =
    [
        "Alder Fleet Services", "Brightway Motors", "Cobalt Car Rental", "Dunmore Tyre Centre",
        "Eastgate Auto Parts", "Fernhill Logistics", "Granite Wheel Co", "Harbour Van Hire",
        "Ironbridge Garages", "Juniper Transport", "Kestrel Motor Group", "Lowland Farm Supply",
    ];

    public static GeneratedOrder[] Orders(PlantParameters parameters, DateOnly startDate, IReadOnlyList<GeneratedProduct> products, SeededRandom random)
    {
        List<GeneratedOrder> orders = [];
        for (int week = 0; week < parameters.HorizonWeeks; week++)
        {
            DateOnly weekStart = startDate.AddDays(week * 7);
            for (int orderInWeek = 0; orderInWeek < parameters.OrdersPerWeek; orderInWeek++)
            {
                bool isRush = random.Chance(parameters.RushShare);
                GeneratedPriority priority = isRush ? GeneratedPriority.High
                    : random.Chance(parameters.LowPriorityShare) ? GeneratedPriority.Low
                    : GeneratedPriority.Normal;
                orders.Add(new GeneratedOrder(
                    string.Create(CultureInfo.InvariantCulture, $"SO-{orders.Count + 1:00000}"),
                    random.Pick(Customers),
                    random.Pick(products).Code,
                    random.Draw(parameters.Quantity),
                    weekStart,
                    weekStart.AddDays(random.Draw(isRush ? parameters.RushDueDays : parameters.DueDays)),
                    priority,
                    isRush));
            }
        }

        return [.. orders];
    }
}
