using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Login_and_Registration_Backend_.NET_.Data;
using Login_and_Registration_Backend_.NET_.Models;
using System.Security.Claims;

namespace Login_and_Registration_Backend_.NET_.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProductionOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductionOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductionOrderDto>>> GetProductionOrders()
        {
            var orders = await _context.ProductionOrders
                .Include(o => o.ProductionJobs)
                .ThenInclude(j => j.Machine)
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            var orderDtos = orders.Select(o => new ProductionOrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerName = o.CustomerName,
                ProductName = o.ProductName,
                Quantity = o.Quantity,
                DueDate = o.DueDate,
                Priority = o.Priority.ToString(),
                Status = o.Status.ToString(),
                Progress = o.Progress,
                EstimatedHours = o.EstimatedHours,
                AssignedMachine = o.AssignedMachine,
                Notes = o.Notes,
                CreatedDate = o.CreatedDate,
                StartDate = o.StartDate,
                CompletedDate = o.CompletedDate,
                CreatedBy = o.CreatedBy,
                DaysUntilDue = (int)(o.DueDate - DateTime.Now).TotalDays,
                ProductionJobs = o.ProductionJobs.Select(j => new ProductionJobDto
                {
                    Id = j.Id,
                    ProductionOrderId = j.ProductionOrderId,
                    JobName = j.JobName,
                    MachineId = j.MachineId,
                    MachineName = j.Machine.Name,
                    Duration = j.Duration,
                    Status = j.Status.ToString(),
                    ScheduledStartTime = j.ScheduledStartTime,
                    ScheduledEndTime = j.ScheduledEndTime,
                    ActualStartTime = j.ActualStartTime,
                    ActualEndTime = j.ActualEndTime,
                    Operator = j.Operator,
                    Notes = j.Notes,
                    SortOrder = j.SortOrder
                }).OrderBy(j => j.SortOrder).ToList()
            }).ToList();

            return Ok(orderDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductionOrderDto>> GetProductionOrder(int id)
        {
            var order = await _context.ProductionOrders
                .Include(o => o.ProductionJobs)
                .ThenInclude(j => j.Machine)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            var orderDto = new ProductionOrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                ProductName = order.ProductName,
                Quantity = order.Quantity,
                DueDate = order.DueDate,
                Priority = order.Priority.ToString(),
                Status = order.Status.ToString(),
                Progress = order.Progress,
                EstimatedHours = order.EstimatedHours,
                AssignedMachine = order.AssignedMachine,
                Notes = order.Notes,
                CreatedDate = order.CreatedDate,
                StartDate = order.StartDate,
                CompletedDate = order.CompletedDate,
                CreatedBy = order.CreatedBy,
                DaysUntilDue = (int)(order.DueDate - DateTime.Now).TotalDays,
                IsOverdue = order.IsOverdue,
                ProductionJobs = order.ProductionJobs.Select(j => new ProductionJobDto
                {
                    Id = j.Id,
                    ProductionOrderId = j.ProductionOrderId,
                    JobName = j.JobName,
                    MachineId = j.MachineId,
                    MachineName = j.Machine.Name,
                    Duration = j.Duration,
                    Status = j.Status.ToString(),
                    ScheduledStartTime = j.ScheduledStartTime,
                    ScheduledEndTime = j.ScheduledEndTime,
                    ActualStartTime = j.ActualStartTime,
                    ActualEndTime = j.ActualEndTime,
                    Operator = j.Operator,
                    Notes = j.Notes,
                    SortOrder = j.SortOrder
                }).OrderBy(j => j.SortOrder).ToList()
            };

            return Ok(orderDto);
        }        [HttpPost]
        public async Task<ActionResult<ProductionOrderDto>> CreateProductionOrder(CreateProductionOrderRequest request)
        {
            Console.WriteLine("CreateProductionOrder called");
            Console.WriteLine($"Request: {System.Text.Json.JsonSerializer.Serialize(request)}");
            
            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState is invalid");
                Console.WriteLine($"Errors: {string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
                return BadRequest(ModelState);
            }

            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
            Console.WriteLine($"User: {username}");
            
            // Generate order number
            var orderCount = await _context.ProductionOrders.CountAsync();
            var orderNumber = $"PO-{DateTime.Now.Year}-{(orderCount + 1):000}";
            Console.WriteLine($"Generated order number: {orderNumber}");

            var order = new ProductionOrder
            {
                OrderNumber = orderNumber,
                CustomerName = request.CustomerName,
                ProductName = request.ProductName,
                Quantity = request.Quantity,
                DueDate = request.DueDate,
                Priority = Enum.Parse<Priority>(request.Priority),
                Status = OrderStatus.Pending,
                Progress = 0,
                EstimatedHours = request.EstimatedHours,
                Notes = request.Notes,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = username
            };

            Console.WriteLine("Adding order to context");
            _context.ProductionOrders.Add(order);
            
            Console.WriteLine("Saving changes");
            await _context.SaveChangesAsync();
            Console.WriteLine($"Order saved with ID: {order.Id}");

            // Create and return the DTO
            var orderDto = new ProductionOrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                ProductName = order.ProductName,
                Quantity = order.Quantity,
                DueDate = order.DueDate,
                Priority = order.Priority.ToString(),
                Status = order.Status.ToString(),
                Progress = order.Progress,
                EstimatedHours = order.EstimatedHours,
                AssignedMachine = order.AssignedMachine,
                Notes = order.Notes,
                CreatedDate = order.CreatedDate,
                StartDate = order.StartDate,
                CompletedDate = order.CompletedDate,
                CreatedBy = order.CreatedBy,
                DaysUntilDue = (int)(order.DueDate - DateTime.Now).TotalDays,
                ProductionJobs = new List<ProductionJobDto>()
            };

            Console.WriteLine("Returning created order");
            return CreatedAtAction(nameof(GetProductionOrder), new { id = order.Id }, orderDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProductionOrder(int id, UpdateProductionOrderRequest request)
        {
            var order = await _context.ProductionOrders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            // Update only provided fields
            if (!string.IsNullOrEmpty(request.CustomerName))
                order.CustomerName = request.CustomerName;
            
            if (!string.IsNullOrEmpty(request.ProductName))
                order.ProductName = request.ProductName;
            
            if (request.Quantity.HasValue)
                order.Quantity = request.Quantity.Value;
            
            if (request.DueDate.HasValue)
                order.DueDate = request.DueDate.Value;
            
            if (!string.IsNullOrEmpty(request.Priority))
                order.Priority = Enum.Parse<Priority>(request.Priority);
            
            if (!string.IsNullOrEmpty(request.Status))
            {
                order.Status = Enum.Parse<OrderStatus>(request.Status);
                
                // Update dates based on status change
                if (request.Status == "InProgress" && order.StartDate == null)
                    order.StartDate = DateTime.UtcNow;
                
                if (request.Status == "Completed" && order.CompletedDate == null)
                {
                    order.CompletedDate = DateTime.UtcNow;
                    order.Progress = 100;
                }
            }
            
            if (request.Progress.HasValue)
                order.Progress = request.Progress.Value;
            
            if (request.EstimatedHours.HasValue)
                order.EstimatedHours = request.EstimatedHours.Value;
            
            if (request.AssignedMachine != null)
                order.AssignedMachine = request.AssignedMachine;
            
            if (request.Notes != null)
                order.Notes = request.Notes;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductionOrderExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductionOrder(int id)
        {
            var order = await _context.ProductionOrders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            _context.ProductionOrders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetStatistics()
        {
            var totalOrders = await _context.ProductionOrders.CountAsync();
            var activeOrders = await _context.ProductionOrders.CountAsync(o => o.Status == OrderStatus.InProgress);
            var completedOrders = await _context.ProductionOrders.CountAsync(o => o.Status == OrderStatus.Completed);
            var pendingOrders = await _context.ProductionOrders.CountAsync(o => o.Status == OrderStatus.Pending);
            var delayedOrders = await _context.ProductionOrders.CountAsync(o => o.Status == OrderStatus.Delayed);
            var completedToday = await _context.ProductionOrders.CountAsync(o => 
                o.Status == OrderStatus.Completed && o.CompletedDate.HasValue && 
                o.CompletedDate.Value.Date == DateTime.UtcNow.Date);

            // Calculate average efficiency (could be more sophisticated)
            var inProgressOrders = await _context.ProductionOrders
                .Where(o => o.Status == OrderStatus.InProgress)
                .ToListAsync();
            
            var efficiency = inProgressOrders.Any() 
                ? inProgressOrders.Average(o => o.Progress) 
                : 0;

            return Ok(new
            {
                totalOrders,
                activeOrders,
                completedOrders,
                pendingOrders,
                delayedOrders,
                completedToday,
                efficiency = Math.Round(efficiency, 1)
            });
        }

        private bool ProductionOrderExists(int id)
        {
            return _context.ProductionOrders.Any(e => e.Id == id);
        }
    }
}
