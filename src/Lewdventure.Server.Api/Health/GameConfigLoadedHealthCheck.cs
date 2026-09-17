using Microsoft.Extensions.Diagnostics.HealthChecks;
using Server.GameConfigs;

namespace Server.Api.Health
{
    internal sealed class GameConfigLoadedHealthCheck : IHealthCheck
    {
        private readonly ConfigSnapshotHasher _configSnapshotHasher;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;

        public GameConfigLoadedHealthCheck(ConfigSnapshotHasher configSnapshotHasher, IGameConfigSetProvider gameConfigSetProvider)
        {
            _configSnapshotHasher = configSnapshotHasher;
            _gameConfigSetProvider = gameConfigSetProvider;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var current = _gameConfigSetProvider.Current;

            if (current.IsEmpty)
                return Task.FromResult(HealthCheckResult.Unhealthy("game configs are not loaded"));

            return Task.FromResult(HealthCheckResult.Healthy($"version = {_configSnapshotHasher.ToShortVersion(current.Version)} source = {current.Source}"));
        }
    }
}
