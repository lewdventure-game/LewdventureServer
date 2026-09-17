using Microsoft.Extensions.Diagnostics.HealthChecks;
using Server.Services;

namespace Server.Api.Health
{
    internal sealed class GameConfigLoadedHealthCheck : IHealthCheck
    {
        private readonly IConfigDistributor _configDistributor;

        public GameConfigLoadedHealthCheck(IConfigDistributor configDistributor)
        {
            _configDistributor = configDistributor;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var constantsCount = _configDistributor.Constants.Collection.Count;

            if (constantsCount <= 0)
                return Task.FromResult(HealthCheckResult.Unhealthy("game configs are not loaded"));

            return Task.FromResult(HealthCheckResult.Healthy($"constants = {constantsCount}"));
        }
    }
}
