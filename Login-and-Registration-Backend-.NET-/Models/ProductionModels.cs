using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Login_and_Registration_Backend_.NET_.Models
{
    public class ProductionOrder
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string ProductName { get; set; } = string.Empty;
        
        [Required]
        public int Quantity { get; set; }
        
        [Required]
        public DateTime DueDate { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Priority { get; set; } = "medium"; // high, medium, low
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "pending"; // pending, in-progress, completed, delayed, cancelled
        
        [Range(0, 100)]
        public int Progress { get; set; } = 0;
        
        public decimal EstimatedHours { get; set; }
        
        [StringLength(50)]
        public string? AssignedMachine { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        public DateTime? StartDate { get; set; }
        
        public DateTime? CompletedDate { get; set; }
        
        [StringLength(50)]
        public string? CreatedBy { get; set; }
        
        // Navigation properties
        public virtual ICollection<ProductionJob> ProductionJobs { get; set; } = new List<ProductionJob>();
    }

    public class ProductionJob
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ProductionOrderId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string JobName { get; set; } = string.Empty;
        
        [Required]
        public int MachineId { get; set; }
        
        public decimal Duration { get; set; } // in hours
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "scheduled"; // scheduled, in-progress, completed, delayed
        
        public DateTime? ScheduledStartTime { get; set; }
        
        public DateTime? ScheduledEndTime { get; set; }
        
        public DateTime? ActualStartTime { get; set; }
        
        public DateTime? ActualEndTime { get; set; }
        
        [StringLength(50)]
        public string? Operator { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        public int SortOrder { get; set; } = 0;
        
        // Navigation properties
        [ForeignKey("ProductionOrderId")]
        public virtual ProductionOrder ProductionOrder { get; set; } = null!;
        
        [ForeignKey("MachineId")]
        public virtual Machine Machine { get; set; } = null!;
    }

    public class Machine
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "idle"; // running, idle, maintenance, error
        
        [Range(0, 100)]
        public int Utilization { get; set; } = 0;
        
        public DateTime? LastMaintenance { get; set; }
        
        public DateTime? NextMaintenance { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual ICollection<ProductionJob> ProductionJobs { get; set; } = new List<ProductionJob>();
    }

    // DTOs for API
    public class CreateProductionOrderRequest
    {
        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string ProductName { get; set; } = string.Empty;
        
        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
        
        [Required]
        public DateTime DueDate { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Priority { get; set; } = "medium";
        
        public decimal EstimatedHours { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class UpdateProductionOrderRequest
    {
        [StringLength(100)]
        public string? CustomerName { get; set; }
        
        [StringLength(100)]
        public string? ProductName { get; set; }
        
        [Range(1, int.MaxValue)]
        public int? Quantity { get; set; }
        
        public DateTime? DueDate { get; set; }
        
        [StringLength(20)]
        public string? Priority { get; set; }
        
        [StringLength(20)]
        public string? Status { get; set; }
        
        [Range(0, 100)]
        public int? Progress { get; set; }
        
        public decimal? EstimatedHours { get; set; }
        
        [StringLength(50)]
        public string? AssignedMachine { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class ProductionOrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime DueDate { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
        public decimal EstimatedHours { get; set; }
        public string? AssignedMachine { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? CreatedBy { get; set; }
        public int DaysUntilDue { get; set; }
        public List<ProductionJobDto> ProductionJobs { get; set; } = new List<ProductionJobDto>();
    }

    public class ProductionJobDto
    {
        public int Id { get; set; }
        public int ProductionOrderId { get; set; }
        public string JobName { get; set; } = string.Empty;
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public decimal Duration { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ScheduledStartTime { get; set; }
        public DateTime? ScheduledEndTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public string? Operator { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }

    public class MachineDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Utilization { get; set; }
        public DateTime? LastMaintenance { get; set; }
        public DateTime? NextMaintenance { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public string? CurrentJob { get; set; }
    }
}
