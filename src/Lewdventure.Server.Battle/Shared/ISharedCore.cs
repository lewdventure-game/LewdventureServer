using Server.Battles;
using Server.Services;

namespace Server.Shared
{
    public interface ISharedCore
    {
        public string ConfigVersion { get; }

        public IConfigDistributor Configs { get; }

        public IBattleCore Battle { get; }
    }
}
