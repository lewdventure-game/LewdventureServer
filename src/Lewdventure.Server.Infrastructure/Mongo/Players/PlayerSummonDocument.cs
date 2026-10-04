namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerSummonDocument
    {
        public int ConfigId { get; set; }

        public int Copies { get; set; }

        public int Level { get; set; } = 1;

        public int MasteryLevel { get; set; } = 1;

        public List<int> SkillLevels { get; set; } = new();

        public List<int> UnlockedSceneIds { get; set; } = new();

        public DateTime UnlockedAt { get; set; }
    }
}
