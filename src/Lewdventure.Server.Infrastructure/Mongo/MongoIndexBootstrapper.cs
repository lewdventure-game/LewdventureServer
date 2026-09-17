using MongoDB.Bson;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoIndexBootstrapper
    {
        private readonly IEnumerable<IMongoIndexContributor> _contributors;
        private readonly ILogger<MongoIndexBootstrapper> _logger;
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public MongoIndexBootstrapper(
            IEnumerable<IMongoIndexContributor> contributors,
            ILogger<MongoIndexBootstrapper> logger,
            IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _contributors = contributors;
            _logger = logger;
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        public async Task<int> ApplyAsync(CancellationToken cancellationToken)
        {
            var applied = 0;

            foreach (var contributor in _contributors)
            {
                var indexes = contributor.GetIndexes();

                for (int i = 0; i < indexes.Count; i++)
                {
                    var index = indexes[i];
                    var collection = _mongoDatabaseAccessor.GetCollection<BsonDocument>(index.CollectionName);
                    var indexName = await collection.Indexes.CreateOneAsync(index.Model, cancellationToken: cancellationToken);

                    _logger.LogInformation("[Mongo] index ensured collection = {Collection} index = {Index}", index.CollectionName, indexName);

                    applied++;
                }
            }

            return applied;
        }
    }
}
