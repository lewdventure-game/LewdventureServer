using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class IdempotencyRepository : IMongoIndexContributor
    {
        private const int RetentionHours = 48;

        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public IdempotencyRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<IdempotencyDocument> Collection => _mongoDatabaseAccessor.GetCollection<IdempotencyDocument>(MongoCollectionNames.Idempotency);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys.Ascending("createdAt");
            var options = new CreateIndexOptions { Name = "createdAt_ttl", ExpireAfter = TimeSpan.FromHours(RetentionHours) };
            var model = new CreateIndexModel<BsonDocument>(keys, options);

            return new[] { new MongoIndexDefinition(MongoCollectionNames.Idempotency, model) };
        }

        public string CreateKey(string userId, string requestId)
        {
            return userId + ":" + requestId;
        }

        public async Task<IdempotencyDocument?> GetAsync(string userId, string requestId, CancellationToken cancellationToken)
        {
            var key = CreateKey(userId, requestId);

            return await Collection.Find(Builders<IdempotencyDocument>.Filter.Eq(item => item.Id, key)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> TryReserveAsync(IdempotencyDocument document, CancellationToken cancellationToken)
        {
            try
            {
                await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);

                return true;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task CompleteAsync(string userId, string requestId, long resultRev, DateTime now, CancellationToken cancellationToken)
        {
            var filter = Builders<IdempotencyDocument>.Filter.Eq(item => item.Id, CreateKey(userId, requestId));
            var update = Builders<IdempotencyDocument>.Update
                .Set(item => item.ResultRev, resultRev)
                .Set(item => item.UpdatedAt, now);

            await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        }

        public async Task<long> DeleteByUserAsync(string userId, CancellationToken cancellationToken)
        {
            var result = await Collection.DeleteManyAsync(Builders<IdempotencyDocument>.Filter.Eq(item => item.UserId, userId), cancellationToken);

            return result.DeletedCount;
        }

        public async Task ReleaseAsync(string userId, string requestId, CancellationToken cancellationToken)
        {
            await Collection.DeleteOneAsync(Builders<IdempotencyDocument>.Filter.Eq(item => item.Id, CreateKey(userId, requestId)), cancellationToken);
        }
    }
}
