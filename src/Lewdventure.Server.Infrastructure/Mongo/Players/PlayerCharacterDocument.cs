namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerCharacterDocument
    {
        public int ConfigId { get; set; }

        public int Copies { get; set; }

        public int UpgradesApplied { get; set; }

        public List<int> UnlockedSceneIds { get; set; } = new();

        public DateTime UnlockedAt { get; set; }
    }
}
