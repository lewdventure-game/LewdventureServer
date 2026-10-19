using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Balance
{
    internal sealed class BalanceJobRepository : IMongoIndexContributor
    {
        public const int RetentionDays = 7;

        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public BalanceJobRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<BalanceJobDocument> Collection => _mongoDatabaseAccessor.GetCollection<BalanceJobDocument>(MongoCollectionNames.BalanceJobs);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var ttl = new CreateIndexOptions { Name = "createdAt_ttl", ExpireAfter = TimeSpan.FromDays(RetentionDays) };
            var createdKeys = Builders<BsonDocument>.IndexKeys.Ascending("createdAt");
            var statusKeys = Builders<BsonDocument>.IndexKeys.Ascending("status").Ascending("createdAt");

            return new[]
            {
                new MongoIndexDefinition(MongoCollectionNames.BalanceJobs, new CreateIndexModel<BsonDocument>(createdKeys, ttl)),
                new MongoIndexDefinition(MongoCollectionNames.BalanceJobs, new CreateIndexModel<BsonDocument>(statusKeys, new CreateIndexOptions { Name = "status_1_createdAt_1" })),
            };
        }

        public async Task InsertAsync(BalanceJobDocument document, CancellationToken cancellationToken)
        {
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public async Task<BalanceJobDocument?> GetAsync(string jobId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<BalanceJobDocument>.Filter.Eq(item => item.Id, jobId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<long> CountPendingAsync(CancellationToken cancellationToken)
        {
            var filter = Builders<BalanceJobDocument>.Filter.In(item => item.Status, new[] { BalanceJobDocument.QueuedStatus, BalanceJobDocument.RunningStatus });

            return await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        }

        public async Task<BalanceJobDocument?> ClaimNextAsync(string workerId, DateTime now, CancellationToken cancellationToken)
        {
            var filter = Builders<BalanceJobDocument>.Filter.Eq(item => item.Status, BalanceJobDocument.QueuedStatus);
            var update = Builders<BalanceJobDocument>.Update
                .Set(item => item.Status, BalanceJobDocument.RunningStatus)
                .Set(item => item.WorkerId, workerId)
                .Set(item => item.StartedAt, now)
                .Set(item => item.UpdatedAt, now);
            var options = new FindOneAndUpdateOptions<BalanceJobDocument>
            {
                Sort = Builders<BalanceJobDocument>.Sort.Ascending(item => item.CreatedAt),
                ReturnDocument = ReturnDocument.After,
            };

            return await Collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        }

        public async Task UpdateProgressAsync(string jobId, int done, int total, DateTime now, CancellationToken cancellationToken)
        {
            var update = Builders<BalanceJobDocument>.Update
                .Set(item => item.Done, done)
                .Set(item => item.Total, total)
                .Set(item => item.UpdatedAt, now);

            await Collection.UpdateOneAsync(RunningFilter(jobId), update, cancellationToken: cancellationToken);
        }

        public async Task CompleteAsync(string jobId, string result, string runId, DateTime now, CancellationToken cancellationToken)
        {
            var update = Builders<BalanceJobDocument>.Update
                .Set(item => item.Status, BalanceJobDocument.DoneStatus)
                .Set(item => item.Result, result)
                .Set(item => item.RunId, runId)
                .Set(item => item.StatusCode, 200)
                .Set(item => item.FinishedAt, now)
                .Set(item => item.UpdatedAt, now);

            await Collection.UpdateOneAsync(RunningFilter(jobId), update, cancellationToken: cancellationToken);
        }

        public async Task FailAsync(string jobId, string error, int statusCode, DateTime now, CancellationToken cancellationToken)
        {
            var update = Builders<BalanceJobDocument>.Update
                .Set(item => item.Status, BalanceJobDocument.FailedStatus)
                .Set(item => item.Error, error)
                .Set(item => item.StatusCode, statusCode)
                .Set(item => item.FinishedAt, now)
                .Set(item => item.UpdatedAt, now);

            await Collection.UpdateOneAsync(RunningFilter(jobId), update, cancellationToken: cancellationToken);
        }

        public async Task<long> FailStaleAsync(DateTime staleBefore, string error, DateTime now, CancellationToken cancellationToken)
        {
            var filter = Builders<BalanceJobDocument>.Filter.And(
                Builders<BalanceJobDocument>.Filter.Eq(item => item.Status, BalanceJobDocument.RunningStatus),
                Builders<BalanceJobDocument>.Filter.Lt(item => item.UpdatedAt, staleBefore));
            var update = Builders<BalanceJobDocument>.Update
                .Set(item => item.Status, BalanceJobDocument.FailedStatus)
                .Set(item => item.Error, error)
                .Set(item => item.StatusCode, 500)
                .Set(item => item.FinishedAt, now)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);

            return result.ModifiedCount;
        }

        private FilterDefinition<BalanceJobDocument> RunningFilter(string jobId)
        {
            return Builders<BalanceJobDocument>.Filter.And(
                Builders<BalanceJobDocument>.Filter.Eq(item => item.Id, jobId),
                Builders<BalanceJobDocument>.Filter.Eq(item => item.Status, BalanceJobDocument.RunningStatus));
        }
    }
}
