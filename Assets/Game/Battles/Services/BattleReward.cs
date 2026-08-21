namespace Server.Battles
{
    internal readonly struct BattleReward
    {
        private readonly BattleRewardType _type;
        private readonly int _id;
        private readonly string _rewardKey;
        private readonly int _count;

        public BattleRewardType Type => _type;

        public int Id => _id;

        public string RewardKey => _rewardKey;

        public bool HasStringRewardKey => string.IsNullOrEmpty(_rewardKey) == false;

        public int Count => _count;

        public BattleReward(BattleRewardType type, int id, int count)
        {
            _type = type;
            _id = id;
            _rewardKey = string.Empty;
            _count = count;
        }

        public BattleReward(BattleRewardType type, string rewardKey, int count)
        {
            _type = type;
            _id = 0;
            _rewardKey = rewardKey;
            _count = count;
        }
    }
}
