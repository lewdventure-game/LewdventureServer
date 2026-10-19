using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Balance
{
    internal sealed class BalanceScenarioRepository
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public BalanceScenarioRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<BalanceScenarioDocument> Collection => _mongoDatabaseAccessor.GetCollection<BalanceScenarioDocument>(MongoCollectionNames.BalanceScenarios);

        public async Task<BalanceScenarioDocument?> GetAsync(string scenarioId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<BalanceScenarioDocument>.Filter.Eq(item => item.Id, scenarioId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<BalanceScenarioDocument>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<BalanceScenarioDocument>.Filter.Empty)
                .SortBy(item => item.Name)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task UpsertAsync(BalanceScenarioDocument document, CancellationToken cancellationToken)
        {
            var filter = Builders<BalanceScenarioDocument>.Filter.Eq(item => item.Id, document.Id);

            await Collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }

        public async Task<long> DeleteAsync(string scenarioId, CancellationToken cancellationToken)
        {
            var result = await Collection.DeleteOneAsync(Builders<BalanceScenarioDocument>.Filter.Eq(item => item.Id, scenarioId), cancellationToken);

            return result.DeletedCount;
        }
    }
}
