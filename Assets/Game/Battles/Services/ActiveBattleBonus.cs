using Server.Bonuses;

namespace Server.Battles
{
    internal sealed class ActiveBattleBonus
    {
        public int BonusId { get; }

        public int Count { get; set; }

        public BonusType BonusType { get; }

        public float BonusValue { get; }

        public BonusOperatorType OperatorType { get; }

        public BonusWorkMode WorkMode { get; }

        public string SourceKey { get; }

        public int EveryTurnStacks { get; set; }

        public int RemainingBattles { get; set; }

        public ActiveBattleBonus(
            int bonusId,
            int count,
            BonusType bonusType,
            float bonusValue,
            BonusOperatorType operatorType,
            BonusWorkMode workMode,
            string sourceKey)
        {
            BonusId = bonusId;
            Count = count;
            BonusType = bonusType;
            BonusValue = bonusValue;
            OperatorType = operatorType;
            WorkMode = workMode;
            SourceKey = sourceKey;
            EveryTurnStacks = 0;
            RemainingBattles = workMode.Kind == BonusWorkModeKind.NextBattles ? workMode.Count : 0;
        }
    }
}
