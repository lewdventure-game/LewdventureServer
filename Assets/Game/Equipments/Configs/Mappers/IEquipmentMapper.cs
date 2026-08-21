using Server.Configs;

namespace Server.Equipments
{
    internal interface IEquipmentMapper : IConfigMapper
    {
        public string Id { get; }

        public string Type { get; }

        public string Rarity { get; }

        public string LevelUpTypes { get; }

        public string LevelUpValues { get; }

        public string EquipmentBonusTypeOne { get; }

        public string EquipmentBonusValuesOne { get; }

        public string EquipmentBonusTypeTwo { get; }

        public string EquipmentBonusValuesTwo { get; }

        public string EquipmentBonusTypeThree { get; }

        public string EquipmentBonusValuesThree { get; }

        public string MergeGroup { get; }

        public string MergeNumber { get; }

        public string MergeRequirements { get; }

        public string ArtName { get; }

        public string IsMelee { get; }

        public string SkillId { get; }
    }
}
