namespace Server.Infrastructure.Mongo
{
    internal interface IMongoDocument
    {
        public string Id { get; }

        public int SchemaVersion { get; }

        public DateTime CreatedAt { get; }

        public DateTime UpdatedAt { get; }
    }
}
