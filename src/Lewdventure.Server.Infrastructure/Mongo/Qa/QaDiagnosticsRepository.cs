using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Qa
{
    internal sealed class QaDiagnosticsRepository : IMongoIndexContributor
    {
        public const int RetentionDays = 7;

        private const int ErrorStatusCode = 400;

        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public QaDiagnosticsRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<RequestTraceDocument> Traces => _mongoDatabaseAccessor.GetCollection<RequestTraceDocument>(MongoCollectionNames.QaRequestTraces);

        private IMongoCollection<ServerErrorDocument> Errors => _mongoDatabaseAccessor.GetCollection<ServerErrorDocument>(MongoCollectionNames.QaServerErrors);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var ttl = new CreateIndexOptions { Name = "createdAt_ttl", ExpireAfter = TimeSpan.FromDays(RetentionDays) };
            var userKeys = Builders<BsonDocument>.IndexKeys.Ascending("userId").Descending("createdAt");
            var correlationKeys = Builders<BsonDocument>.IndexKeys.Ascending("correlationId");
            var createdKeys = Builders<BsonDocument>.IndexKeys.Ascending("createdAt");

            return new[]
            {
                new MongoIndexDefinition(MongoCollectionNames.QaRequestTraces, new CreateIndexModel<BsonDocument>(createdKeys, ttl)),
                new MongoIndexDefinition(MongoCollectionNames.QaRequestTraces, new CreateIndexModel<BsonDocument>(userKeys, new CreateIndexOptions { Name = "userId_1_createdAt_-1" })),
                new MongoIndexDefinition(MongoCollectionNames.QaRequestTraces, new CreateIndexModel<BsonDocument>(correlationKeys, new CreateIndexOptions { Name = "correlationId_1" })),
                new MongoIndexDefinition(MongoCollectionNames.QaServerErrors, new CreateIndexModel<BsonDocument>(createdKeys, ttl)),
                new MongoIndexDefinition(MongoCollectionNames.QaServerErrors, new CreateIndexModel<BsonDocument>(userKeys, new CreateIndexOptions { Name = "userId_1_createdAt_-1" })),
                new MongoIndexDefinition(MongoCollectionNames.QaServerErrors, new CreateIndexModel<BsonDocument>(correlationKeys, new CreateIndexOptions { Name = "correlationId_1" })),
            };
        }

        public async Task InsertTracesAsync(List<RequestTraceDocument> traces, CancellationToken cancellationToken)
        {
            await Traces.InsertManyAsync(traces, new InsertManyOptions { IsOrdered = false }, cancellationToken);
        }

        public async Task InsertErrorsAsync(List<ServerErrorDocument> errors, CancellationToken cancellationToken)
        {
            await Errors.InsertManyAsync(errors, new InsertManyOptions { IsOrdered = false }, cancellationToken);
        }

        public async Task<List<RequestTraceDocument>> ListTracesAsync(string userId, string correlationId, bool errorsOnly, int limit, CancellationToken cancellationToken)
        {
            var filters = new List<FilterDefinition<RequestTraceDocument>>();

            if (string.IsNullOrEmpty(userId) == false)
                filters.Add(Builders<RequestTraceDocument>.Filter.Eq(item => item.UserId, userId));

            if (string.IsNullOrEmpty(correlationId) == false)
                filters.Add(Builders<RequestTraceDocument>.Filter.Eq(item => item.CorrelationId, correlationId));

            if (errorsOnly)
                filters.Add(Builders<RequestTraceDocument>.Filter.Gte(item => item.StatusCode, ErrorStatusCode));

            var filter = filters.Count == 0 ? Builders<RequestTraceDocument>.Filter.Empty : Builders<RequestTraceDocument>.Filter.And(filters);

            return await Traces.Find(filter).SortByDescending(item => item.CreatedAt).Limit(limit).ToListAsync(cancellationToken);
        }

        public async Task<List<ServerErrorDocument>> ListErrorsAsync(string userId, string correlationId, int limit, CancellationToken cancellationToken)
        {
            var filters = new List<FilterDefinition<ServerErrorDocument>>();

            if (string.IsNullOrEmpty(userId) == false)
                filters.Add(Builders<ServerErrorDocument>.Filter.Eq(item => item.UserId, userId));

            if (string.IsNullOrEmpty(correlationId) == false)
                filters.Add(Builders<ServerErrorDocument>.Filter.Eq(item => item.CorrelationId, correlationId));

            var filter = filters.Count == 0 ? Builders<ServerErrorDocument>.Filter.Empty : Builders<ServerErrorDocument>.Filter.And(filters);

            return await Errors.Find(filter).SortByDescending(item => item.CreatedAt).Limit(limit).ToListAsync(cancellationToken);
        }

        public async Task<long> DeleteByUserAsync(string userId, CancellationToken cancellationToken)
        {
            var traces = await Traces.DeleteManyAsync(Builders<RequestTraceDocument>.Filter.Eq(item => item.UserId, userId), cancellationToken);
            var errors = await Errors.DeleteManyAsync(Builders<ServerErrorDocument>.Filter.Eq(item => item.UserId, userId), cancellationToken);

            return traces.DeletedCount + errors.DeletedCount;
        }
    }
}
