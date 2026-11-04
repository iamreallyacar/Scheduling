using Microsoft.Extensions.Diagnostics.HealthChecks;
using Login_and_Registration_Backend_.NET_.Services;

namespace Login_and_Registration_Backend_.NET_.HealthChecks
{
    public class JwtServiceHealthCheck : IHealthCheck
    {
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;

        public JwtServiceHealthCheck(IJwtService jwtService, IConfiguration configuration)
        {
            _jwtService = jwtService;
            _configuration = configuration;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if JWT configuration is available
                var jwtKey = _configuration["Jwt:Key"];
                var jwtIssuer = _configuration["Jwt:Issuer"];
                var jwtAudience = _configuration["Jwt:Audience"];

                if (string.IsNullOrWhiteSpace(jwtKey) || 
                    string.IsNullOrWhiteSpace(jwtIssuer) || 
                    string.IsNullOrWhiteSpace(jwtAudience))
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy("JWT configuration is incomplete"));
                }

                // Additional checks can be added here, such as:
                // - Testing token generation with a dummy user
                // - Validating key length and strength
                // - Checking external dependencies if any

                return Task.FromResult(HealthCheckResult.Healthy("JWT service is configured correctly"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("JWT service health check failed", ex));
            }
        }
    }
}
