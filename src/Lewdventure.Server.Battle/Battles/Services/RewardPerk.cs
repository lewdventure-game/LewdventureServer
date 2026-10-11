using Server.Perks;

namespace Server.Battles
{
    internal sealed class RewardPerk : BasePerk
    {
        internal RewardPerk(IPerkMapper mapper)
            : base(mapper)
        {
        }

        public override bool CanTrigger(int currentTurn)
        {
            return false;
        }
    }
}
