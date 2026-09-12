using Server.Configs;

namespace Server.Entities
{
    internal interface ICharacterMapper : IConfigMapper
    {
        public int Id { get; }

        public bool IsMelee { get; }

        public int[] UpgradeCosts { get; }

        public int StartBonusId { get; }

        public int StartBonusValue { get; }

        public int[] UpgradeBonusTypes { get; }

        public float[] UpgradeBonusValues { get; }

        public string ArtName { get; }

        public int[] SkillIds { get; }
    }
}
