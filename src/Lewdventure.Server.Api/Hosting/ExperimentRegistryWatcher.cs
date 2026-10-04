using Server.Infrastructure.Experiments;

namespace Server.Api.Hosting
{
    internal sealed class ExperimentRegistryWatcher : BackgroundService
    {
        private const int PollIntervalSeconds = 30;

        private readonly ExperimentRegistryLoader _experimentRegistryLoader;
        private readonly ILogger<ExperimentRegistryWatcher> _logger;

        public ExperimentRegistryWatcher(ExperimentRegistryLoader experimentRegistryLoader, ILogger<ExperimentRegistryWatcher> logger)
        {
            _experimentRegistryLoader = experimentRegistryLoader;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(PollIntervalSeconds));

            while (await timer.WaitForNextTickAsync(stoppingToken))
                await PollAsync(stoppingToken);
        }

        private async Task PollAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _experimentRegistryLoader.ReloadAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is TimeoutException || exception is MongoDB.Driver.MongoException)
            {
                _logger.LogWarning("[Experiment] registry poll failed error = {Error}", exception.Message);
            }
        }
    }
}
