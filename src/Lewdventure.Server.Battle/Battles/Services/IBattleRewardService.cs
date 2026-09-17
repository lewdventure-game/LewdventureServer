namespace Server.Battles
{
    internal interface IBattleRewardService
    {
        public IReadOnlyList<BattleReward> Parse(string value);

        public void Apply(
            IReadOnlyList<BattleReward> rewards,
            IUnitState source,
            IUnitState target,
            List<BattleCommand> commands,
            int currentTurn);

        public void Apply(
            IReadOnlyList<BattleReward> rewards,
            IUnitState source,
            IUnitState target,
            ITeamSimulationState? attacker,
            ITeamSimulationState? defender,
            List<BattleCommand> commands,
            int currentTurn);
    }
}
