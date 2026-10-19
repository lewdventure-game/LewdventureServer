namespace Server.Infrastructure.Mongo.Balance
{
    internal sealed class BalanceScenarioDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Name { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;

        public string UpdatedBy { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }
}
