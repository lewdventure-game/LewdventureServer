namespace Server.Admin.Backend.Models
{
    public sealed class PlayerUnitModel
    {
        public int Id { get; set; }

        public int Copies { get; set; }

        public int Level { get; set; }

        public int PromoteLevel { get; set; }

        public int MasteryLevel { get; set; }

        public List<int> SkillLevels { get; set; } = new();
    }
}
