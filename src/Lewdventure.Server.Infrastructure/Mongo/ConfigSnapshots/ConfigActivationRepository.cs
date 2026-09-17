using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigActivationRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;
        private readonly IMongoTransactionRunner _mongoTransactionRunner;

        public ConfigActivationRepository(IMongoDatabaseAccessor mongoDatabaseAccessor, IMongoTransactionRunner mongoTransactionRunner)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
            _mongoTransactionRunner = mongoTransactionRunner;
        }

        private IMongoCollection<ConfigStateDocument> States => _mongoDatabaseAccessor.GetCollection<ConfigStateDocument>(MongoCollectionNames.ConfigState);

        private IMongoCollection<ConfigActivationDocument> Activations => _mongoDatabaseAccessor.GetCollection<ConfigActivationDocument>(MongoCollectionNames.ConfigActivations);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys.Descending("createdAt");
            var model = new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = "createdAt_-1" });

            return new[] { new MongoIndexDefinition(MongoCollectionNames.ConfigActivations, model) };
        }

        public async Task EnsureCollectionsAsync(CancellationToken cancellationToken)
        {
            var names = await (await _mongoDatabaseAccessor.Database.ListCollectionNamesAsync(cancellationToken: cancellationToken)).ToListAsync(cancellationToken);

            if (names.Contains(MongoCollectionNames.ConfigState) == false)
                await _mongoDatabaseAccessor.Database.CreateCollectionAsync(MongoCollectionNames.ConfigState, cancellationToken: cancellationToken);

            if (names.Contains(MongoCollectionNames.ConfigActivations) == false)
                await _mongoDatabaseAccessor.Database.CreateCollectionAsync(MongoCollectionNames.ConfigActivations, cancellationToken: cancellationToken);
        }

        public async Task<string> GetActiveVersionAsync(CancellationToken cancellationToken)
        {
            var state = await States.Find(Builders<ConfigStateDocument>.Filter.Eq(item => item.Id, ConfigStateDocument.ActiveId)).FirstOrDefaultAsync(cancellationToken);

            return state == null ? string.Empty : state.Version;
        }

        public async Task<string> ActivateAsync(string version, string activatedBy, string reason, CancellationToken cancellationToken)
        {
            await EnsureCollectionsAsync(cancellationToken);

            return await _mongoTransactionRunner.ExecuteAsync(async (session, token) =>
            {
                var filter = Builders<ConfigStateDocument>.Filter.Eq(item => item.Id, ConfigStateDocument.ActiveId);
                var current = await States.Find(session, filter).FirstOrDefaultAsync(token);
                var previousVersion = current == null ? string.Empty : current.Version;
                var now = DateTime.UtcNow;
                var activation = new ConfigActivationDocument
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = now,
                    UpdatedAt = now,
                    Version = version,
                    PreviousVersion = previousVersion,
                    ActivatedBy = activatedBy,
                    Reason = reason,
                };
                var state = new ConfigStateDocument
                {
                    CreatedAt = current == null ? now : current.CreatedAt,
                    UpdatedAt = now,
                    Version = version,
                    ActivationId = activation.Id,
                };

                await Activations.InsertOneAsync(session, activation, cancellationToken: token);
                await States.ReplaceOneAsync(session, filter, state, new ReplaceOptions { IsUpsert = true }, token);

                return previousVersion;
            }, cancellationToken);
        }
    }
}
