using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Mongo.Qa
{
    internal sealed class QaTemplateDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;

        public string SourceUserId { get; set; } = string.Empty;

        public string ConfigVersion { get; set; } = string.Empty;

        public PlayerProfileDocument Profile { get; set; } = new();
    }
}
