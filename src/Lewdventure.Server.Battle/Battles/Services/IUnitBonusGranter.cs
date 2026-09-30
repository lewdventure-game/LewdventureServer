using Server.Entities;

namespace Server.Battles
{
    internal interface IUnitBonusGranter
    {
        public void GrantBuildBonus(IUnitState unitState, int bonusId, string sourceKey);

        public void GrantTrainingBonuses(UnitState unitState, int trainingLevel);

        public void GrantEquipmentBonuses(UnitState unitState, IUnitSnapshot unitSnapshot);

        public void GrantArtifactBonuses(UnitState unitState, IUnitSnapshot unitSnapshot);

        public void GrantAspectBonuses(UnitState unitState, IUnitSnapshot unitSnapshot);

        public void GrantSnapshotRunBonuses(IUnitState unitState, IUnitSnapshot unitSnapshot);

        public void GrantSummonAccountBonuses(IUnitState mainUnit, IUnitSnapshot summonSnapshot);
    }
}
