namespace Server.Infrastructure.Mongo.Balance
{
    internal sealed class BalanceRunDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Actor { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string ConfigVersion { get; set; } = string.Empty;

        public int Edits { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }
}
