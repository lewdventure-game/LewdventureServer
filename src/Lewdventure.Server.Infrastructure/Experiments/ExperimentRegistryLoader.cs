using Server.Infrastructure.Mongo.Experiments;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentRegistryLoader
    {
        private readonly ExperimentRegistry _experimentRegistry;
        private readonly ExperimentRepository _experimentRepository;
        private readonly ILogger<ExperimentRegistryLoader> _logger;

        public ExperimentRegistryLoader(
            ExperimentRegistry experimentRegistry,
            ExperimentRepository experimentRepository,
            ILogger<ExperimentRegistryLoader> logger)
        {
            _experimentRegistry = experimentRegistry;
            _experimentRepository = experimentRepository;
            _logger = logger;
        }

        public async Task ReloadAsync(CancellationToken cancellationToken)
        {
            var previousCount = _experimentRegistry.Running.Count;
            var running = await _experimentRepository.ListRunningAsync(cancellationToken);

            _experimentRegistry.Replace(running);

            if (previousCount != running.Count)
                _logger.LogInformation("[Experiment] registry reloaded running = {Running}", running.Count);
        }
    }
}
