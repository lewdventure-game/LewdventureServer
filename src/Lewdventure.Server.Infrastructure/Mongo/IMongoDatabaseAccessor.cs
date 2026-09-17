using MongoDB.Driver;

namespace Server.Infrastructure.Mongo
{
    internal interface IMongoDatabaseAccessor
    {
        public IMongoClient Client { get; }

        public IMongoDatabase Database { get; }

        public IMongoCollection<TDocument> GetCollection<TDocument>(string name);
    }
}
