namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerSummonDocument
    {
        public int ConfigId { get; set; }

        public int Copies { get; set; }

        public int Level { get; set; } = 1;

        public int MasteryLevel { get; set; }

        public long SkillExpSpent { get; set; }

        public DateTime UnlockedAt { get; set; }
    }
}
