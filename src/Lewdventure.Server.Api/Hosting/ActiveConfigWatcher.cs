using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Server.Api.Hosting
{
    internal sealed class ActiveConfigWatcher : BackgroundService
    {
        private readonly ConfigPublishingService _configPublishingService;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;
        private readonly ILogger<ActiveConfigWatcher> _logger;
        private readonly GameConfigOptions _options;

        public ActiveConfigWatcher(
            ConfigPublishingService configPublishingService,
            IGameConfigSetProvider gameConfigSetProvider,
            ILogger<ActiveConfigWatcher> logger,
            IOptions<GameConfigOptions> options)
        {
            _configPublishingService = configPublishingService;
            _gameConfigSetProvider = gameConfigSetProvider;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

            while (await timer.WaitForNextTickAsync(stoppingToken))
                await PollAsync(stoppingToken);
        }

        private async Task PollAsync(CancellationToken stoppingToken)
        {
            try
            {
                var activeVersion = await _configPublishingService.GetActiveVersionAsync(stoppingToken);

                if (string.IsNullOrEmpty(activeVersion) || string.Equals(activeVersion, _gameConfigSetProvider.Current.Version, StringComparison.Ordinal))
                    return;

                var result = await _configPublishingService.LoadActiveAsync(string.Empty, stoppingToken);

                if (result.Succeeded == false)
                    _logger.LogWarning("[Config][Snapshot] poll reload failed version = {Version} errors = {Errors}", activeVersion, string.Join("; ", result.Errors));
            }
            catch (Exception exception) when (exception is TimeoutException || exception is MongoDB.Driver.MongoException)
            {
                _logger.LogWarning("[Config][Snapshot] poll failed error = {Error}", exception.Message);
            }
        }
    }
}
