using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Experiments
{
    internal sealed class ExperimentRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public ExperimentRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<ExperimentDocument> Collection => _mongoDatabaseAccessor.GetCollection<ExperimentDocument>(MongoCollectionNames.Experiments);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys.Ascending("status").Descending("createdAt");
            var model = new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = "status_1_createdAt_-1" });

            return new[] { new MongoIndexDefinition(MongoCollectionNames.Experiments, model) };
        }

        public async Task<ExperimentDocument?> GetAsync(string experimentId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<ExperimentDocument>.Filter.Eq(item => item.Id, experimentId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<ExperimentDocument>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<ExperimentDocument>.Filter.Empty)
                .SortByDescending(item => item.CreatedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ExperimentDocument>> ListRunningAsync(CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<ExperimentDocument>.Filter.Eq(item => item.Status, ExperimentDocument.RunningStatus))
                .SortBy(item => item.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> InsertIfMissingAsync(ExperimentDocument document, CancellationToken cancellationToken)
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

        public async Task<bool> ReplaceAsync(ExperimentDocument document, long expectedRev, CancellationToken cancellationToken)
        {
            var filter = Builders<ExperimentDocument>.Filter.And(
                Builders<ExperimentDocument>.Filter.Eq(item => item.Id, document.Id),
                Builders<ExperimentDocument>.Filter.Eq(item => item.Rev, expectedRev));
            var result = await Collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }

        public async Task<bool> DeleteDraftAsync(string experimentId, CancellationToken cancellationToken)
        {
            var filter = Builders<ExperimentDocument>.Filter.And(
                Builders<ExperimentDocument>.Filter.Eq(item => item.Id, experimentId),
                Builders<ExperimentDocument>.Filter.Eq(item => item.Status, ExperimentDocument.DraftStatus));
            var result = await Collection.DeleteOneAsync(filter, cancellationToken);

            return 0 < result.DeletedCount;
        }
    }
}
