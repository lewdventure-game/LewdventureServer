using MongoDB.Bson;
using MongoDB.Driver;
using Server.GameConfigs;

namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigSnapshotRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public ConfigSnapshotRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<ConfigSnapshotDocument> Collection => _mongoDatabaseAccessor.GetCollection<ConfigSnapshotDocument>(MongoCollectionNames.ConfigSnapshots);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var keys = Builders<BsonDocument>.IndexKeys.Descending("createdAt");
            var model = new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = "createdAt_-1" });

            return new[] { new MongoIndexDefinition(MongoCollectionNames.ConfigSnapshots, model) };
        }

        public async Task<bool> InsertIfMissingAsync(GameConfigSnapshot snapshot, string createdBy, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var document = new ConfigSnapshotDocument
            {
                Id = snapshot.Version,
                FormatVersion = GameConfigSnapshot.CurrentFormatVersion,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = createdBy,
                SourceKind = snapshot.SourceKind,
                SnapshotCreatedAt = snapshot.CreatedAt,
            };

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                document.Domains.Add(new ConfigSnapshotDomainDocument
                {
                    Domain = domain.Domain,
                    SpreadsheetId = domain.SpreadsheetId,
                    Range = domain.Range,
                    RowsJson = domain.RowsJson,
                });
            }

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

        public async Task<GameConfigSnapshot?> GetAsync(string version, CancellationToken cancellationToken)
        {
            var document = await Collection.Find(Builders<ConfigSnapshotDocument>.Filter.Eq(item => item.Id, version)).FirstOrDefaultAsync(cancellationToken);

            if (document == null)
                return null;

            var domains = new List<ConfigSnapshotDomain>(document.Domains.Count);

            for (int i = 0; i < document.Domains.Count; i++)
            {
                var domain = document.Domains[i];

                domains.Add(new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, domain.RowsJson));
            }

            return new GameConfigSnapshot(document.Id, document.SnapshotCreatedAt, document.SourceKind, domains);
        }

        public async Task<List<ConfigSnapshotSummary>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            var projection = Builders<ConfigSnapshotDocument>.Projection.Exclude(item => item.Domains);
            var documents = await Collection.Find(FilterDefinition<ConfigSnapshotDocument>.Empty)
                .SortByDescending(item => item.CreatedAt)
                .Limit(limit)
                .Project<ConfigSnapshotDocument>(projection)
                .ToListAsync(cancellationToken);
            var result = new List<ConfigSnapshotSummary>(documents.Count);

            for (int i = 0; i < documents.Count; i++)
                result.Add(new ConfigSnapshotSummary(documents[i].Id, documents[i].CreatedAt, documents[i].CreatedBy, documents[i].SourceKind));

            return result;
        }
    }
}
