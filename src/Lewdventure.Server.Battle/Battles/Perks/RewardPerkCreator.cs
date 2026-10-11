using Server.Perks;

namespace Server.Battles
{
    internal sealed class RewardPerkCreator : IPerkCreator
    {
        public PerkType PerkType => PerkType.Reward;

        public string TypeKey => "reward";

        public IPerk Create(IPerkMapper mapper)
        {
            return new RewardPerk(mapper);
        }
    }
}
