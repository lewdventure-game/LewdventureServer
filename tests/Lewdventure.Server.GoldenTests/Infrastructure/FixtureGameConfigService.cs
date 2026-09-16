using Server.Services;

namespace Tests.Golden.Infrastructure
{
    internal sealed class FixtureGameConfigService : IGameConfigService
    {
        private readonly IConfigDistributor _configDistributor;
        private readonly ConfigDistributorFiller _configDistributorFiller;
        private readonly ConfigSnapshotLoader _configSnapshotLoader;
        private readonly GoldenPaths _goldenPaths;

        public FixtureGameConfigService(
            IConfigDistributor configDistributor,
            ConfigDistributorFiller configDistributorFiller,
            ConfigSnapshotLoader configSnapshotLoader,
            GoldenPaths goldenPaths)
        {
            _configDistributor = configDistributor;
            _configDistributorFiller = configDistributorFiller;
            _configSnapshotLoader = configSnapshotLoader;
            _goldenPaths = goldenPaths;
        }

        public Task<(bool Success, string ErrorMessage)> UpdateAllConfigsAsync(bool isDevEnvironment)
        {
            var snapshot = _configSnapshotLoader.Load(_goldenPaths.FixturePath);

            _configDistributorFiller.Fill(_configDistributor, snapshot);

            return Task.FromResult((true, string.Empty));
        }
    }
}
