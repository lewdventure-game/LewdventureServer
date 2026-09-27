namespace Server.Battles
{
    internal interface IUnitLoadoutBinder
    {
        public List<IPerk> BuildPerks(IUnitSnapshot unitSnapshot);

        public IReadOnlyList<ISkill> BuildSkills(IUnitSnapshot unitSnapshot, bool isSummon);

        public void RegisterSnapshotEquippedEntities(UnitState unitState, IUnitSnapshot unitSnapshot, bool isSummon, BattleSide battleSide);

        public void ApplyEquippedPerks(IUnitState unitState);

        public void SeedActiveStatuses(UnitState unitState, IUnitSnapshot unitSnapshot, bool isSummon);
    }
}
