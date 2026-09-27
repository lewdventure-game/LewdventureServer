namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerProfileDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public long Rev { get; set; }

        public Dictionary<string, long> Resources { get; set; } = new();

        public List<PlayerCharacterDocument> Characters { get; set; } = new();

        public List<PlayerSummonDocument> Summons { get; set; } = new();

        public List<PlayerEquipmentDocument> Equipment { get; set; } = new();

        public PlayerLoadoutDocument Loadout { get; set; } = new();

        public PlayerStoryDocument Story { get; set; } = new();

        public Dictionary<string, int> Flags { get; set; } = new();

        public Dictionary<string, string> Settings { get; set; } = new();
    }
}
