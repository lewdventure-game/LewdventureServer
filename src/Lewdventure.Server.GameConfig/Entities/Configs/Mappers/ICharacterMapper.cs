using Server.Configs;

namespace Server.Entities
{
    public interface ICharacterMapper : IConfigMapper
    {
        public int Id { get; }

        public bool IsMelee { get; }

        public int PromoteId { get; }

        public int[] SkillIds { get; }

        public int[] PromoteToSkillLevels { get; }

        public string ArtName { get; }
    }
}
