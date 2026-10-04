using Server.GameConfigs;

namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class CachedGameConfigSet
    {
        public CachedGameConfigSet(GameConfigSet configSet, long lastUsed)
        {
            ConfigSet = configSet;
            LastUsed = lastUsed;
        }

        public GameConfigSet ConfigSet { get; }

        public long LastUsed { get; set; }
    }
}
