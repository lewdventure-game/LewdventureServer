using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerLedgerRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public PlayerLedgerRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<PlayerLedgerDocument> Collection => _mongoDatabaseAccessor.GetCollection<PlayerLedgerDocument>(MongoCollectionNames.PlayerLedger);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys.Ascending("userId").Descending("createdAt");
            var model = new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = "userId_1_createdAt_-1" });

            return new[] { new MongoIndexDefinition(MongoCollectionNames.PlayerLedger, model) };
        }

        public async Task AppendAsync(PlayerLedgerDocument document, CancellationToken cancellationToken)
        {
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public async Task<long> DeleteByUserAsync(string userId, CancellationToken cancellationToken)
        {
            var result = await Collection.DeleteManyAsync(Builders<PlayerLedgerDocument>.Filter.Eq(item => item.UserId, userId), cancellationToken);

            return result.DeletedCount;
        }

        public async Task<List<PlayerLedgerDocument>> ListAsync(string userId, int limit, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<PlayerLedgerDocument>.Filter.Eq(item => item.UserId, userId))
                .SortByDescending(item => item.CreatedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }
    }
}
