namespace Server.Battles
{
    internal interface IUnitSnapshot
    {
        public int Id { get; set; }

        public int Level { get; set; }

        public int MasteryLevel { get; set; }

        public List<IEquipmentSnapshot> Equipments { get; set; }

        public List<int> EquipmentIds { get; set; }

        public int TrainingLevel { get; set; }

        public List<int> ArtifactIds { get; set; }

        public List<int> AspectIds { get; set; }

        public List<int> ActivePerkIds { get; set; }

        public List<string> ActiveSkillIds { get; set; }

        public List<int> ActiveStatusIds { get; set; }

        public List<IBonusGrantSnapshot> ActiveBonuses { get; set; }

        public int SlotIndex { get; set; }
    }
}
