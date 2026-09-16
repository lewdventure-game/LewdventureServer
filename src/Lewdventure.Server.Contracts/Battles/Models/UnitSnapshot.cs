namespace Server.Battles
{
    internal sealed class UnitSnapshot : IUnitSnapshot
    {
        public int Id { get; set; }

        public int Level { get; set; }

        public int MasteryLevel { get; set; }

        public List<IEquipmentSnapshot> Equipments { get; set; } = new();

        public List<int> EquipmentIds { get; set; } = new();

        public int TrainingLevel { get; set; }

        public List<int> ArtifactIds { get; set; } = new();

        public List<int> AspectIds { get; set; } = new();

        public List<int> ActivePerkIds { get; set; } = new();

        public List<string> ActiveSkillIds { get; set; } = new();

        public List<int> ActiveStatusIds { get; set; } = new();

        public List<IBonusGrantSnapshot> ActiveBonuses { get; set; } = new();

        public int SlotIndex { get; set; }
    }
}
