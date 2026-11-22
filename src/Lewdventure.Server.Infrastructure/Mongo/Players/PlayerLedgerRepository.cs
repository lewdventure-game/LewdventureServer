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

        public async Task AppendAsync(IClientSessionHandle session, PlayerLedgerDocument document, CancellationToken cancellationToken)
        {
            await Collection.InsertOneAsync(session, document, cancellationToken: cancellationToken);
        }

        public async Task<bool> ExistsAsync(IClientSessionHandle session, string id, CancellationToken cancellationToken)
        {
            return await Collection.Find(session, item => item.Id == id).Limit(1).AnyAsync(cancellationToken);
        }

        public Task<List<string>> ListExistingIdsAsync(List<string> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
                return Task.FromResult(new List<string>());

            var filter = Builders<PlayerLedgerDocument>.Filter.In(item => item.Id, ids);

            return Collection.Find(filter).Project(item => item.Id).ToListAsync(cancellationToken);
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
