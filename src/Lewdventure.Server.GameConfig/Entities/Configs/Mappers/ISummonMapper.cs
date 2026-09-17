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

        public float AttackSpeed { get; }

        public int AttackCooldown { get; }

        public bool IsMelee { get; }

        public string[] SkillIds { get; }

        public int MasteryId { get; }

        public int LevelUpgradePattern { get; }
    }
}
