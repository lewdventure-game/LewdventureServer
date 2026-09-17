using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoIndexDefinition
    {
        public MongoIndexDefinition(string collectionName, CreateIndexModel<BsonDocument> model)
        {
            CollectionName = collectionName;
            Model = model;
        }

        public string CollectionName { get; }

        public CreateIndexModel<BsonDocument> Model { get; }
    }
}
