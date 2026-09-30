using Server.Configs;

namespace Server.Entities
{
    public interface ICharacterMapper : IConfigMapper
    {
        public int Id { get; }

        public bool IsMelee { get; }

        public int[] UpgradeCosts { get; }

        public int StartBonusId { get; }

        public int[] UpgradeBonusIds { get; }

        public string ArtName { get; }

        public int[] SkillIds { get; }
    }
}
