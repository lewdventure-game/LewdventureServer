using MongoDB.Bson;
using MongoDB.Driver;

namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class UserRepository : IMongoIndexContributor
    {
        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public UserRepository(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        private IMongoCollection<UserDocument> Collection => _mongoDatabaseAccessor.GetCollection<UserDocument>(MongoCollectionNames.Users);

        public IReadOnlyList<MongoIndexDefinition> GetIndexes()
        {
            var deviceKeys = Builders<BsonDocument>.IndexKeys.Ascending("devices.deviceIdHash");
            var deviceModel = new CreateIndexModel<BsonDocument>(deviceKeys, new CreateIndexOptions { Name = "devices.deviceIdHash_1", Unique = true, Sparse = true });
            var identityKeys = Builders<BsonDocument>.IndexKeys.Ascending("identities.provider").Ascending("identities.subject");
            var identityModel = new CreateIndexModel<BsonDocument>(identityKeys, new CreateIndexOptions { Name = "identities_1", Unique = true, Sparse = true });
            var experimentKeys = Builders<BsonDocument>.IndexKeys.Ascending("experiment.experimentId").Ascending("experiment.groupId");
            var experimentModel = new CreateIndexModel<BsonDocument>(experimentKeys, new CreateIndexOptions { Name = "experiment_1", Sparse = true });
            var qaKeys = Builders<BsonDocument>.IndexKeys.Ascending("qa.alias");
            var qaModel = new CreateIndexModel<BsonDocument>(qaKeys, new CreateIndexOptions { Name = "qa.alias_1", Unique = true, Sparse = true });

            return new[]
            {
                new MongoIndexDefinition(MongoCollectionNames.Users, deviceModel),
                new MongoIndexDefinition(MongoCollectionNames.Users, identityModel),
                new MongoIndexDefinition(MongoCollectionNames.Users, experimentModel),
                new MongoIndexDefinition(MongoCollectionNames.Users, qaModel),
            };
        }

        public async Task<UserExperimentDocument?> GetExperimentAsync(string userId, CancellationToken cancellationToken)
        {
            return await Collection
                .Find(Builders<UserDocument>.Filter.Eq(item => item.Id, userId))
                .Project(item => item.Experiment)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserDocument?> GetAnalyticsProjectionAsync(string userId, CancellationToken cancellationToken)
        {
            var projection = Builders<UserDocument>.Projection
                .Include(item => item.Experiment)
                .Include(item => item.LastCountry);

            return await Collection
                .Find(Builders<UserDocument>.Filter.Eq(item => item.Id, userId))
                .Project<UserDocument>(projection)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> UpdateExperimentAsync(
            string userId,
            UserExperimentDocument? expected,
            UserExperimentDocument? assignment,
            List<string> seenExperimentIds,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq(item => item.Id, userId),
                CreateExperimentFilter(expected));
            var update = Builders<UserDocument>.Update
                .AddToSetEach(item => item.ExperimentsSeen, seenExperimentIds)
                .Set(item => item.UpdatedAt, now);

            if (assignment != null)
                update = update.Set(item => item.Experiment, assignment);

            var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }

        public async Task<long> CountInGroupAsync(string experimentId, string groupId, CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq("experiment.experimentId", experimentId),
                Builders<UserDocument>.Filter.Eq("experiment.groupId", groupId),
                Builders<UserDocument>.Filter.Eq(item => item.Qa, null));

            return await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        }

        public async Task<UserDocument?> GetAsync(string userId, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<UserDocument>.Filter.Eq(item => item.Id, userId)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserDocument?> FindByDeviceAsync(string deviceIdHash, CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.ElemMatch(item => item.Devices, device => device.DeviceIdHash == deviceIdHash);

            return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserDocument?> FindByQaAliasAsync(string alias, CancellationToken cancellationToken)
        {
            return await Collection.Find(Builders<UserDocument>.Filter.Eq("qa.alias", alias)).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<List<UserDocument>> ListQaAsync(int limit, CancellationToken cancellationToken)
        {
            var projection = Builders<UserDocument>.Projection
                .Include(item => item.Qa)
                .Include(item => item.CreatedAt)
                .Include(item => item.UpdatedAt);

            return await Collection
                .Find(Builders<UserDocument>.Filter.Ne(item => item.Qa, null))
                .Project<UserDocument>(projection)
                .SortBy(item => item.Qa!.Alias)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> MarkQaAsync(string userId, UserQaDocument qa, DateTime now, CancellationToken cancellationToken)
        {
            var update = Builders<UserDocument>.Update
                .Set(item => item.Qa, qa)
                .Set(item => item.Experiment, null)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(Builders<UserDocument>.Filter.Eq(item => item.Id, userId), update, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }

        public async Task<bool> UnmarkQaAsync(string userId, DateTime now, CancellationToken cancellationToken)
        {
            var update = Builders<UserDocument>.Update
                .Unset(item => item.Qa)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(Builders<UserDocument>.Filter.Eq(item => item.Id, userId), update, cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
                return false;

            var forcedFilter = Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq(item => item.Id, userId),
                Builders<UserDocument>.Filter.Eq("experiment.forced", true));

            await Collection.UpdateOneAsync(forcedFilter, Builders<UserDocument>.Update.Set(item => item.Experiment, null), cancellationToken: cancellationToken);

            return true;
        }

        public async Task<bool> SetQaExperimentAsync(string userId, UserExperimentDocument? assignment, DateTime now, CancellationToken cancellationToken)
        {
            var filter = Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq(item => item.Id, userId),
                Builders<UserDocument>.Filter.Ne(item => item.Qa, null));
            var update = Builders<UserDocument>.Update
                .Set(item => item.Experiment, assignment)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }

        public async Task<long> DeleteAsync(string userId, CancellationToken cancellationToken)
        {
            var result = await Collection.DeleteOneAsync(Builders<UserDocument>.Filter.Eq(item => item.Id, userId), cancellationToken);

            return result.DeletedCount;
        }

        public async Task InsertAsync(UserDocument document, CancellationToken cancellationToken)
        {
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public async Task<bool> UpsertDeviceAsync(string userId, UserDeviceDocument device, string country, DateTime now, CancellationToken cancellationToken)
        {
            var removeFilter = Builders<UserDocument>.Filter.Eq(item => item.Id, userId);
            var pull = Builders<UserDocument>.Update
                .PullFilter(item => item.Devices, existing => existing.DeviceIdHash == device.DeviceIdHash)
                .Set(item => item.LastCountry, country)
                .Set(item => item.UpdatedAt, now);

            await Collection.UpdateOneAsync(removeFilter, pull, cancellationToken: cancellationToken);

            var push = Builders<UserDocument>.Update
                .Push(item => item.Devices, device)
                .Set(item => item.UpdatedAt, now);
            var result = await Collection.UpdateOneAsync(removeFilter, push, cancellationToken: cancellationToken);

            return 0 < result.MatchedCount;
        }

        private FilterDefinition<UserDocument> CreateExperimentFilter(UserExperimentDocument? expected)
        {
            if (expected == null)
                return Builders<UserDocument>.Filter.Eq(item => item.Experiment, null);

            return Builders<UserDocument>.Filter.And(
                Builders<UserDocument>.Filter.Eq("experiment.experimentId", expected.ExperimentId),
                Builders<UserDocument>.Filter.Eq("experiment.groupId", expected.GroupId));
        }
    }
}
