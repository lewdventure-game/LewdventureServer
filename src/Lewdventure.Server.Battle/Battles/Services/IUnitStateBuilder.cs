namespace Server.Battles
{
    internal interface IUnitStateBuilder
    {
        public IUnitState Build(IUnitSnapshot unitSnapshot, BattleSide battleSide, bool isSummon, int storyLevelId, int stageId);

        public void GrantSummonAccountBonuses(IUnitState mainUnit, IUnitSnapshot summonSnapshot);
    }
}
