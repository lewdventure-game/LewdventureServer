using Server.Common;
using Server.Configs;

namespace Server.Entities
{
    internal interface ISummonMapper : IConfigMapper
    {
        public int Id { get; }

        public string ArtName { get; }

        public RarityType Rarity { get; }

        public float[] DamageOnLevels { get; }

        public float[] BreakoutMultipliers { get; }

        public string SkillId { get; }

        public int MasteryId { get; }

        public int LevelUpgradePattern { get; }

        public int[] BonusMasteryLevels { get; }

        public int[] BonusTypes { get; }

        public string IsMelee { get; }
    }
}
