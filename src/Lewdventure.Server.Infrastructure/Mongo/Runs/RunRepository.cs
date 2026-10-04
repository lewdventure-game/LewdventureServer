using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public RunRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<RunDocument> Collection => _mongoDatabaseAccessor.GetCollection<RunDocument>(MongoCollectionNames.PlayerRuns);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var activeKeys = Builders<BsonDocument>.IndexKeys.Ascending("userId").Ascending("status");
            var activeModel = new CreateIndexModel<BsonDocument>(activeKeys, new CreateIndexOptions { Name = "userId_1_status_1" });
            var historyKeys = Builders<BsonDocument>.IndexKeys.Ascending("userId").Descending("createdAt");
            var historyModel = new CreateIndexModel<BsonDocument>(historyKeys, new CreateIndexOptions { Name = "userId_1_createdAt_-1" });

            return new[]
            {
                new MongoIndexDefinition(MongoCollectionNames.PlayerRuns, activeModel),
                new MongoIndexDefinition(MongoCollectionNames.PlayerRuns, historyModel),
            };
        }

        public async Task<RunDocument?> GetAsync(string runId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<RunDocument>.Filter.Eq(item => item.Id, runId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<RunDocument?> GetActiveAsync(string userId, CancellationToken cancellationToken)
        {
            var filter = Builders<RunDocument>.Filter.And(
                Builders<RunDocument>.Filter.Eq(item => item.UserId, userId),
                Builders<RunDocument>.Filter.Eq(item => item.Status, RunDocument.ActiveStatus));

            return await Collection.Find(filter).SortByDescending(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<string> GetActiveConfigVersionAsync(string userId, CancellationToken cancellationToken)
        {
            var filter = Builders<RunDocument>.Filter.And(
                Builders<RunDocument>.Filter.Eq(item => item.UserId, userId),
                Builders<RunDocument>.Filter.Eq(item => item.Status, RunDocument.ActiveStatus));
            var version = await Collection
                .Find(filter)
                .SortByDescending(item => item.CreatedAt)
                .Project(item => item.ConfigVersion)
                .FirstOrDefaultAsync(cancellationToken);

            return version ?? string.Empty;
        }

        public async Task<List<RunDocument>> ListAsync(string userId, int limit, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<RunDocument>.Filter.Eq(item => item.UserId, userId))
                .SortByDescending(item => item.CreatedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<long> DeleteByUserAsync(string userId, CancellationToken cancellationToken)
        {
            var result = await Collection.DeleteManyAsync(Builders<RunDocument>.Filter.Eq(item => item.UserId, userId), cancellationToken);

            return result.DeletedCount;
        }

        public async Task InsertAsync(RunDocument document, CancellationToken cancellationToken)
        {
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceAsync(RunDocument document, long expectedRev, CancellationToken cancellationToken)
        {
            var filter = Builders<RunDocument>.Filter.And(
                Builders<RunDocument>.Filter.Eq(item => item.Id, document.Id),
                Builders<RunDocument>.Filter.Eq(item => item.Rev, expectedRev));
            var result = await Collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }
    }
}
