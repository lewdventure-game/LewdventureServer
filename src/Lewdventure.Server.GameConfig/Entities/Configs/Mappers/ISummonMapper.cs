using Server.Common;
using Server.Configs;

namespace Server.Entities
{
    public interface ISummonMapper : IConfigMapper
    {
        public int Id { get; }

        public string ArtName { get; }

        public RarityType Rarity { get; }

        public float[] DamageOnLevels { get; }

        public int AttackCooldown { get; }

        public bool IsMelee { get; }

        public string[] SkillIds { get; }

        public int[] MasteryForSkills { get; }

        public int[] SkillUpgradeIds { get; }

        public int MasteryId { get; }

        public int LevelPatternId { get; }
    }
}
