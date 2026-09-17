using MongoDB.Bson;
using MongoDB.Driver;
using Server.Infrastructure.Mongo;

namespace Tests.Integration.Mongo
{
    internal sealed class TestIndexContributor : IMongoIndexContributor
    {
        public const string CollectionName = "it_items";

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys.Ascending("createdAt");
            var model = new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = "createdAt_1" });

            return new[] { new MongoIndexDefinition(CollectionName, model) };
        }
    }
}
