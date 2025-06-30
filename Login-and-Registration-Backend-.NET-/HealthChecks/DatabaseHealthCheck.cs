using Microsoft.Extensions.Diagnostics.HealthChecks;
using Login_and_Registration_Backend_.NET_.Data;

namespace Login_and_Registration_Backend_.NET_.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly ApplicationDbContext _context;

        public DatabaseHealthCheck(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to connect to the database
                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
                
                if (canConnect)
                {
                    return HealthCheckResult.Healthy("Database connection is healthy");
                }
                else
                {
                    return HealthCheckResult.Unhealthy("Cannot connect to database");
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database health check failed", ex);
            }
        }
    }
}
