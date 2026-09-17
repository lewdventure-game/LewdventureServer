namespace Server.Battles
{
    internal interface IBattleRewardParser
    {
        public IReadOnlyList<BattleReward> Parse(string value);

        public IReadOnlyList<RewardBonus> ParseBonuses(string value);
    }
}
