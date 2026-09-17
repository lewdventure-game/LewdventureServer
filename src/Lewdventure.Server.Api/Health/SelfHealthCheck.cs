using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Server.Api.Health
{
    internal sealed class SelfHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy("process is running"));
        }
    }
}
