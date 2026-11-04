using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Configuration
{
    /// <summary>
    /// Configuration model for database seeding
    /// </summary>
    public class SeedConfiguration
    {
        public List<MachineConfig> Machines { get; set; } = new();
        public List<OrderConfig> Orders { get; set; } = new();
        public List<JobConfig> Jobs { get; set; } = new();
        public bool SeedRelationships { get; set; } = true;
        public bool EnableCleanup { get; set; } = false;
    }

    /// <summary>
    /// Configuration for seeding machines
    /// </summary>
    public class MachineConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = "idle";
        public int Utilization { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public int LastMaintenanceDaysAgo { get; set; } = 30;
        public int NextMaintenanceDaysAhead { get; set; } = 30;
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Configuration for seeding production orders
    /// </summary>
    public class OrderConfig
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Priority { get; set; } = "Medium";
        public decimal EstimatedHours { get; set; }
        public string Status { get; set; } = "Pending";
        public int DueDateDaysAhead { get; set; } = 7;
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
    }

    /// <summary>
    /// Configuration for seeding production jobs
    /// </summary>
    public class JobConfig
    {
        public string JobName { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public decimal Duration { get; set; }
        public string Status { get; set; } = "Scheduled";
        public int ScheduledStartDaysAhead { get; set; } = 1;
        public string? Operator { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }
}
