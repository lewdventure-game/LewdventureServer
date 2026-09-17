using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoDatabaseAccessor : IMongoDatabaseAccessor
    {
        public MongoDatabaseAccessor(IMongoClient client, IOptions<MongoOptions> options)
        {
            Client = client;
            Database = client.GetDatabase(options.Value.DatabaseName);
        }

        public IMongoClient Client { get; }

        public IMongoDatabase Database { get; }

        public IMongoCollection<TDocument> GetCollection<TDocument>(string name)
        {
            return Database.GetCollection<TDocument>(name);
        }
    }
}
