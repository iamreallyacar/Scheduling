using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Login_and_Registration_Backend_.NET_.Data;
using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MachinesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MachinesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MachineDto>>> GetMachines()
        {
            var machines = await _context.Machines
                .Include(m => m.ProductionJobs.Where(j => j.Status == "in-progress"))
                .ThenInclude(j => j.ProductionOrder)
                .Where(m => m.IsActive)
                .ToListAsync();

            var machineDtos = machines.Select(m => new MachineDto
            {
                Id = m.Id,
                Name = m.Name,
                Type = m.Type,
                Status = m.Status,
                Utilization = m.Utilization,
                LastMaintenance = m.LastMaintenance,
                NextMaintenance = m.NextMaintenance,
                Notes = m.Notes,
                IsActive = m.IsActive,
                CurrentJob = m.ProductionJobs.FirstOrDefault()?.JobName
            }).ToList();

            return Ok(machineDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MachineDto>> GetMachine(int id)
        {
            var machine = await _context.Machines
                .Include(m => m.ProductionJobs.Where(j => j.Status == "in-progress"))
                .ThenInclude(j => j.ProductionOrder)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

            if (machine == null)
            {
                return NotFound();
            }

            var machineDto = new MachineDto
            {
                Id = machine.Id,
                Name = machine.Name,
                Type = machine.Type,
                Status = machine.Status,
                Utilization = machine.Utilization,
                LastMaintenance = machine.LastMaintenance,
                NextMaintenance = machine.NextMaintenance,
                Notes = machine.Notes,
                IsActive = machine.IsActive,
                CurrentJob = machine.ProductionJobs.FirstOrDefault()?.JobName
            };

            return Ok(machineDto);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateMachineStatus(int id, [FromBody] UpdateMachineStatusRequest request)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null || !machine.IsActive)
            {
                return NotFound();
            }

            machine.Status = request.Status;
            machine.Utilization = request.Utilization;

            if (!string.IsNullOrEmpty(request.Notes))
            {
                machine.Notes = request.Notes;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetMachineStatistics()
        {
            var machines = await _context.Machines.Where(m => m.IsActive).ToListAsync();
            
            var totalMachines = machines.Count;
            var runningMachines = machines.Count(m => m.Status == "running");
            var idleMachines = machines.Count(m => m.Status == "idle");
            var maintenanceMachines = machines.Count(m => m.Status == "maintenance");
            var errorMachines = machines.Count(m => m.Status == "error");
            var averageUtilization = machines.Any() ? machines.Average(m => m.Utilization) : 0;

            return Ok(new
            {
                totalMachines,
                runningMachines,
                idleMachines,
                maintenanceMachines,
                errorMachines,
                averageUtilization = Math.Round(averageUtilization, 1)
            });
        }
    }

    public class UpdateMachineStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public int Utilization { get; set; }
        public string? Notes { get; set; }
    }
}
