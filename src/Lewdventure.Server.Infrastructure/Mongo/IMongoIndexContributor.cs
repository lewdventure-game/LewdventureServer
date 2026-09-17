namespace Server.Infrastructure.Mongo
{
    internal interface IMongoIndexContributor
    {
        public IReadOnlyList<MongoIndexDefinition> GetIndexes();
    }
}
