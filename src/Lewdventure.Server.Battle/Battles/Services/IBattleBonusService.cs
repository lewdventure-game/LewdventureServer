namespace Server.Battles
{
    internal interface IBattleBonusService
    {
        public void Grant(
            IUnitState unit,
            int bonusId,
            int count,
            string sourceKey,
            List<BattleCommand> commands,
            int currentTurn);

        public void GrantRewardBonuses(
            IUnitState unit,
            IReadOnlyList<RewardBonus> bonuses,
            int stacksMultiplier,
            string sourceKey,
            List<BattleCommand> commands,
            int currentTurn);

        public string CreateFreeSourceKey(IUnitState unit, string sourceKeyPrefix, int firstIndex);

        public void RemoveBySourceKey(
            IUnitState unit,
            string sourceKey,
            List<BattleCommand> commands,
            int currentTurn);

        public void OnTurnStart(IUnitState unit, int currentTurn, List<BattleCommand> commands);

        public void OnBattleEnd(IUnitState unit, List<BattleCommand> commands);

        public void Rebuild(IUnitState unit, int currentTurn, List<BattleCommand> commands, bool emitSetBonusCommands);
    }
}
