using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Experiments
{
    internal sealed class ExperimentChangeRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public ExperimentChangeRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<ExperimentChangeDocument> Collection => _mongoDatabaseAccessor.GetCollection<ExperimentChangeDocument>(MongoCollectionNames.ExperimentChanges);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys.Ascending("experimentId").Descending("createdAt");
            var model = new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = "experimentId_1_createdAt_-1" });

            return new[] { new MongoIndexDefinition(MongoCollectionNames.ExperimentChanges, model) };
        }

        public async Task InsertAsync(ExperimentChangeDocument document, CancellationToken cancellationToken)
        {
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public async Task<List<ExperimentChangeDocument>> ListAsync(string experimentId, int limit, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<ExperimentChangeDocument>.Filter.Eq(item => item.ExperimentId, experimentId))
                .SortByDescending(item => item.CreatedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }
    }
}
