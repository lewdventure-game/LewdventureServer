using Server.Common;
using Server.Configs;

namespace Server.Equipments
{
    public interface IEquipmentMapper : IConfigMapper
    {
        public int Id { get; }

        public EquipmentType Type { get; }

        public RarityType Rarity { get; }

        public int PromoteId { get; }

        public int[] BonusIds { get; }

        public int[] SkillIds { get; }

        public int MergeGroup { get; }

        public int MergeNumber { get; }

        public string MergeRequirements { get; }

        public string ArtName { get; }
    }
}
