using Server.Services;

namespace Server.GameConfigs
{
    internal sealed class GameConfigSetProvider : IGameConfigSetProvider
    {
        private readonly ILogger<GameConfigSetProvider> _logger;

        private GameConfigSet _current = new(GameConfigSet.EmptyVersion, DateTime.MinValue, "none", new ConfigDistributor());

        public GameConfigSetProvider(ILogger<GameConfigSetProvider> logger)
        {
            _logger = logger;
        }

        public GameConfigSet Current => Volatile.Read(ref _current);

        public void Swap(GameConfigSet configSet)
        {
            var previous = Interlocked.Exchange(ref _current, configSet);

            _logger.LogInformation("[Config][Snapshot] swapped from = {PreviousVersion} to = {Version} source = {Source}", previous.Version, configSet.Version, configSet.Source);
        }
    }
}
