using Server.Battles;
using Server.GameConfigs;
using Server.Services;

namespace Server.Shared
{
    internal sealed class SharedCore : ISharedCore
    {
        private readonly IBattleCore _battle;
        private readonly IConfigDistributor _configs;
        private readonly string _configVersion;

        public SharedCore(GameConfigSet gameConfigSet, IBattleCore battle)
        {
            _battle = battle;
            _configs = gameConfigSet.Distributor;
            _configVersion = gameConfigSet.Version;
        }

        public string ConfigVersion => _configVersion;

        public IConfigDistributor Configs => _configs;

        public IBattleCore Battle => _battle;
    }
}
