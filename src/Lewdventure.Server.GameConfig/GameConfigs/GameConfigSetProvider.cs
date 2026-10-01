using Server.Services;

namespace Server.GameConfigs
{
    internal sealed class GameConfigSetProvider : IGameConfigSetProvider
    {
        private const string EmptySource = "none";

        private readonly ICoreLog _coreLog;

        private GameConfigSet _current = new(
            GameConfigSet.EmptyVersion,
            DateTime.MinValue,
            EmptySource,
            new ConfigDistributor(),
            new GameConfigSnapshot(GameConfigSet.EmptyVersion, DateTime.MinValue, EmptySource, Array.Empty<ConfigSnapshotDomain>()));

        public GameConfigSetProvider(ICoreLog coreLog)
        {
            _coreLog = coreLog;
        }

        public GameConfigSet Current => Volatile.Read(ref _current);

        public void Swap(GameConfigSet configSet)
        {
            var previous = Interlocked.Exchange(ref _current, configSet);

            _coreLog.Information($"[Config][Snapshot] swapped from = {previous.Version} to = {configSet.Version} source = {configSet.Source}");
        }
    }
}
