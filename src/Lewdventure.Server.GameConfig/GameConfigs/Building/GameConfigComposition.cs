using Server.Bonuses;

namespace Server.GameConfigs
{
    internal sealed class GameConfigComposition
    {
        private readonly ConfigSnapshotHasher _configSnapshotHasher;
        private readonly GameConfigSetBuilder _gameConfigSetBuilder;

        public GameConfigComposition(ICoreLog coreLog)
        {
            var configRangeReader = new ConfigRangeReader();
            var configRowLocator = new ConfigRowLocator(configRangeReader);
            var configRowsParser = new ConfigRowsParser(configRowLocator);
            var configSnapshotValidator = new ConfigSnapshotValidator(
                new ConfigDomainNames(),
                new EffectParametersValidator(new EffectParameterRegistry()),
                new EnemyDataValidator());

            _configSnapshotHasher = new ConfigSnapshotHasher();
            _gameConfigSetBuilder = new GameConfigSetBuilder(new BonusWorkModeParser(coreLog), configRowsParser, configSnapshotValidator, coreLog);
        }

        public ConfigSnapshotHasher ConfigSnapshotHasher => _configSnapshotHasher;

        public GameConfigSetBuilder GameConfigSetBuilder => _gameConfigSetBuilder;
    }
}
