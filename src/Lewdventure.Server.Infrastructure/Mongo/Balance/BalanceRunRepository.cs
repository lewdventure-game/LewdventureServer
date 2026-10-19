using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Balance
{
    internal sealed class BalanceRunRepository : IMongoIndexContributor
    {
        public const int RetentionDays = 90;

        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public BalanceRunRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<BalanceRunDocument> Collection => _mongoDatabaseAccessor.GetCollection<BalanceRunDocument>(MongoCollectionNames.BalanceRuns);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var ttl = new CreateIndexOptions { Name = "createdAt_ttl", ExpireAfter = TimeSpan.FromDays(RetentionDays) };
            var createdKeys = Builders<BsonDocument>.IndexKeys.Ascending("createdAt");

            return new[]
            {
                new MongoIndexDefinition(MongoCollectionNames.BalanceRuns, new CreateIndexModel<BsonDocument>(createdKeys, ttl)),
            };
        }

        public async Task InsertAsync(BalanceRunDocument document, CancellationToken cancellationToken)
        {
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public async Task<BalanceRunDocument?> GetAsync(string runId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<BalanceRunDocument>.Filter.Eq(item => item.Id, runId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<BalanceRunDocument>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<BalanceRunDocument>.Filter.Empty)
                .SortByDescending(item => item.CreatedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }
    }
}
