namespace Server.Battles
{
    internal readonly struct RewardBonus
    {
        private readonly int _bonusId;
        private readonly int _count;

        public int BonusId => _bonusId;

        public int Count => _count;

        public RewardBonus(int bonusId, int count)
        {
            _bonusId = bonusId;
            _count = count;
        }
    }
}
